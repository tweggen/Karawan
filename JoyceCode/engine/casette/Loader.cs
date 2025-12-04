using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using builtin.baking;
using builtin.map;
using engine.joyce;
using engine.meta;
using engine.world;
using static engine.Logger;

namespace engine.casette;


/**
 * Load the heritage big nogame.json, interpreting domain specific
 * results.
 */
public class Loader
{
    private object _lo = new();
    private Engine _engine;
    
    private JsonElement _jeRoot;
    private string strDefaultLoaderAssembly = "";
    private bool _traceResources = false;
    private IAssetImplementation _iAssetImpl;
    private Mix _mix;
    private bool _isLoaded = false;
    
    //  private SortedDictionary<string, ISerializable> _mapLoaders = new();

    public SortedSet<string> AvailableAnimations = new();

    private List<LoaderSubscription> _whenLoadedList = new();
    
    static private void _setJsonElement(in JsonElement je, Action<object> action)
    {
        switch (je.ValueKind)
        {
            case JsonValueKind.False:
                action(false);
                break;
            case JsonValueKind.String:
                action(je.GetString());
                break;
            case JsonValueKind.True:
                action(true);
                break;
            case JsonValueKind.Number:
                action(je.GetSingle());
                break;
            case JsonValueKind.Undefined:
            case JsonValueKind.Object:
            case JsonValueKind.Null:
            case JsonValueKind.Array:
                break;
        }
    }
    

    private void _loadToSerializable(JsonNode jnCurr, object target)
    {
        _engine = I.Get<Engine>();

        var iSerializable = target as ISerializable;
        if (iSerializable == null)
        {
            Error(
                $"Trying to load config to object of type {target.GetType().Name} failed, not ISerializable.");
            return;
        }

        try
        {
            // Assuming ISerializable.SetupFrom has been adapted to accept JsonNode
            _engine.Run(iSerializable.SetupFrom(jnCurr));
        }
        catch (Exception e)
        {
            Error($"Unable to load from config: {e}.");
        }
    }


    private void _loadToSerializable(string cassettePath, object target)
    {
        string[] pathParts = cassettePath.Split('.');
        JsonNode jnCurr = _jnRoot;
        bool isInvalid = false;

        foreach (var part in pathParts)
        {
            if (jnCurr is JsonObject obj && obj.TryGetPropertyValue(part, out var jnNextPart))
            {
                jnCurr = jnNextPart;
            }
            else
            {
                isInvalid = true;
                Error($"Unable to resolve config path {cassettePath}: unable to find {part}.");
                break;
            }
        }

        if (isInvalid)
        {
            return;
        }

        _loadToSerializable(jnCurr, target);
    }





    private void _loadTextureAtlas(JsonElement jeAtlas)
    {
        var tc = I.Get<TextureCatalogue>();
        string atlasTag = jeAtlas.GetProperty("tag").GetString();
        string atlasUri = jeAtlas.GetProperty("uri").GetString();

        if (_traceResources) Trace($"LoadTextureAtlas: Added Resource \"{atlasTag}\" from {atlasUri}.");
        string pathProbe = Path.Combine(engine.GlobalSettings.Get("Engine.ResourcePath"), atlasUri); 
        if (!File.Exists(pathProbe))
        {
            Trace($"Warning: resource file for {pathProbe} does not exist.");
        }
        _iAssetImpl.AddAssociation(atlasTag, atlasUri);

        
        var jeTextures = jeAtlas.GetProperty("textures");
        foreach (var pairTexture in jeTextures.EnumerateObject())
        {
            JsonElement jet = pairTexture.Value;
            string textureTag = pairTexture.Name;
            float u = jet.GetProperty("u").GetSingle();
            float v = jet.GetProperty("v").GetSingle();
            float uScale = jet.GetProperty("uScale").GetSingle();
            float vScale = jet.GetProperty("vScale").GetSingle();
            tc.AddAtlasEntry(
                textureTag, atlasTag, 
                new Vector2(u,v), 
                new Vector2(uScale, vScale),
                jet.GetProperty("width").GetInt32(),
                jet.GetProperty("height").GetInt32(),
                jeAtlas.GetProperty("isMipmap").GetBoolean()
                );
        }
    }

    
    /**
     * Read the textures: each of the object keys contains the resource
     * tag for a json file containing the texture atlas. We shall try
     * to open that one.
     */
    private void _loadTextures(JsonElement jeTextures)
    {
        try
        {
            if (jeTextures.TryGetProperty("channels", out var jpChannels))
            {

                foreach (var kvpChannels in jpChannels.EnumerateObject())
                {
                    JsonElement jeChannelDesc = kvpChannels.Value;
                    string file = jeChannelDesc.GetProperty("file").GetString();
                    try
                    {
                        JsonDocument jdocAtlas = JsonDocument.Parse(
                            engine.Assets.Open(file), new()
                            {
                                AllowTrailingCommas = true
                            });
                        var jeAtlas = jdocAtlas.RootElement;

                        JsonElement jeAtlasList = jeAtlas.GetProperty("atlasses");
                        foreach (var pairAtlas in jeAtlasList.EnumerateObject())
                        {
                            _loadTextureAtlas(pairAtlas.Value);
                        }
                    }
                    catch (Exception e)
                    {
                        Warning($"Unable to parse resource object for texture: {e}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Warning($"Unable to load textures: {e}");
        }
    }
    

       private void _loadGlobalSettings(JsonElement jeGlobalSettings)
    {
        try
        {
            foreach (var pair in jeGlobalSettings.EnumerateObject())
            {
                try
                {
                    engine.GlobalSettings.Set(pair.Name, pair.Value.ToString());
                }
                catch (Exception e)
                {
                    Warning($"Error setting properties {pair.Name}: {e}");
                }
            }
        }
        catch (Exception e)
        {
            Warning($"Error reading properties: {e}");
        }
    }


    private void _loadProperties(JsonElement jeProperties)
    {
        try
        {
            foreach (var pair in jeProperties.EnumerateObject())
            {
                try
                {
                    _setJsonElement(pair.Value, o => engine.Props.Set(pair.Name, o));
                }
                catch (Exception e)
                {
                    Warning($"Error setting global setting {pair.Name}: {e}");
                }
            }
        }
        catch (Exception e)
        {
            Warning($"Error reading global settings: {e}");
        }
    }


    enum CreationType
    {
        Undefined = 0,
        FactoryMethod,
        Constructor
    }

    /**
     * Return a factory method according to the description in the element.
     */
    public Func<object> CreateFactoryMethod(string? key, JsonNode jnValue)
    {
        string strClassName = default;
        string strMethodName = default;
        CreationType creationType = CreationType.Undefined;
        Action<object>? setupProperties = null;
        bool haveConfig = false;
        JsonNode jnConfig = null;
        string cassettePath = null;

        if (jnValue is JsonObject obj)
        {
            if (obj.TryGetPropertyValue("config", out var configNode) && configNode is JsonObject)
            {
                jnConfig = configNode;
                haveConfig = true;
            }

            if (obj.TryGetPropertyValue("implementation", out var implNode))
            {
                string? strImplementation = implNode?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(strImplementation))
                {
                    strMethodName = strImplementation;
                    creationType = CreationType.FactoryMethod;
                }
            }

            if (obj.TryGetPropertyValue("className", out var classNode))
            {
                string? strClassNameCand = classNode?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(strClassNameCand))
                {
                    strClassName = strClassNameCand;
                    creationType = CreationType.Constructor;
                }
            }

            if (obj.TryGetPropertyValue("cassettePath", out var cassetteNode))
            {
                cassettePath = cassetteNode?.GetValue<string>();
            }

            if (obj.TryGetPropertyValue("properties", out var propsNode) && propsNode is JsonObject jnProperties)
            {
                setupProperties = (object instance) =>
                {
                    foreach (var pair in jnProperties)
                    {
                        PropertyInfo prop = instance.GetType().GetProperty(
                            pair.Key, BindingFlags.Public | BindingFlags.Instance);
                        if (prop != null && prop.CanWrite)
                        {
                            JsonNode val = pair.Value;
                            if (val == null) continue;

                            if (val is JsonValue jv)
                            {
                                if (jv.TryGetValue<string>(out var s))
                                    prop.SetValue(instance, s, null);
                                else if (jv.TryGetValue<double>(out var d))
                                    prop.SetValue(instance, (float)d, null);
                            }
                            else if (val is JsonObject nestedObj)
                            {
                                var dict = new SortedDictionary<string, string>();
                                foreach (var kvpDict in nestedObj)
                                {
                                    dict[kvpDict.Key] = kvpDict.Value?.GetValue<string>();
                                }

                                prop.SetValue(instance, dict, null);
                            }
                        }
                    }
                };
            }
        }

        if (creationType == CreationType.Undefined)
        {
            if (key != null)
            {
                strClassName = key;
                creationType = CreationType.Constructor;
            }
        }

        switch (creationType)
        {
            default:
            case CreationType.Undefined:
                ErrorThrow<ArgumentException>($"Invalid factory definition for {jnValue.ToJsonString()}");
                return () => null;

            case CreationType.Constructor:
                return () =>
                {
                    var instance = engine.rom.Loader.LoadClass(
                        strDefaultLoaderAssembly, strClassName);
                    setupProperties?.Invoke(instance);
                    if (haveConfig) _loadToSerializable(jnConfig, instance);
                    return instance;
                };

            case CreationType.FactoryMethod:
                return () =>
                {
                    int lastDot = strMethodName.LastIndexOf('.');
                    if (lastDot == -1)
                    {
                        ErrorThrow(
                            $"Invalid implementation name string \"{strMethodName}\": Does not contain a last dot to mark the method.",
                            m => new ArgumentException(m));
                    }

                    string className = strMethodName.Substring(0, lastDot);
                    if (className.Length == 0)
                    {
                        ErrorThrow($"Invalid empty class name \"{strMethodName}\".",
                            m => new ArgumentException(m));
                    }

                    string methodName = strMethodName.Substring(lastDot + 1);
                    if (methodName.Length == 0)
                    {
                        ErrorThrow($"Invalid empty method name \"{strMethodName}\".",
                            m => new ArgumentException(m));
                    }

                    Type t = Type.GetType(className);
                    if (t == null)
                    {
                        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            t = a.GetType(className);
                            if (t != null) break;
                        }
                    }

                    if (t == null)
                    {
                        ErrorThrow($"Class \"{className}\" not found.",
                            m => new ArgumentException(m));
                    }

                    var methodInfo = t.GetMethod(methodName);
                    if (methodInfo == null)
                    {
                        ErrorThrow(
                            $"Method \"{methodName}\" not found in class \"{className}\".",
                            m => new ArgumentException(m));
                    }

                    var instance = methodInfo.Invoke(null, Array.Empty<object>());
                    setupProperties?.Invoke(instance);
                    if (haveConfig) _loadToSerializable(jnConfig, instance);
                    if (cassettePath != null) _loadToSerializable(cassettePath, instance);
                    return instance;
                };
        }
    }

    private void _loadScenes(JsonElement je)
    {
        var sceneSequencer = I.Get<SceneSequencer>();
        if (je.TryGetProperty("catalogue", out var jeCatalogue))
        {
            sceneSequencer.AddFrom(jeCatalogue);
        }
        sceneSequencer.SetMainScene(je.GetProperty("startup").GetString());
    }

    private void _loadImplementations(JsonNode jnImplementations)
    {
        if (engine.GlobalSettings.Get("joyce.CompileMode") == "true")
        {
            ErrorThrow<InvalidOperationException>("I should not have been called.");
            return;
        }

        try
        {
            /*
             * Register the listed class factories, possibly using the key as interface name.
             */
            if (jnImplementations is JsonObject obj)
            {
                foreach (var pair in obj)
                {
                    var factoryMethod = I.Get<engine.casette.Loader>().CreateFactoryMethod(pair.Key, pair.Value);

                    /*
                     * We are loading the implementations. The key name definitely is the
                     * name we register the implementation for.
                     */
                    string interfaceName = pair.Key;

                    try
                    {
                        Type type = engine.rom.Loader.LoadType(strDefaultLoaderAssembly, interfaceName);
                        I.Instance.RegisterFactory(type, factoryMethod);
                    }
                    catch (Exception e)
                    {
                        Warning($"Unable to load implementation type {pair.Key}: {e}");
                    }
                }
            }
        }
        catch (Exception e)
        {

        }
    }

    private void _loadGameConfig(JsonElement je)
    {
        if (je.TryGetProperty("globalSettings", out var jeGlobalSettings))
        {
            _loadGlobalSettings(jeGlobalSettings);
        }

        if (je.TryGetProperty("properties", out var jeProperties))
        {
            _loadProperties(jeProperties);
        }

        /*
         * If I'm running in compile mode I am not supposed to load
         * all the implementations that follow.
         */
        if (engine.GlobalSettings.Get("joyce.CompileMode") != "true")
        {
            if (je.TryGetProperty("implementations", out var jeImplementations))
            {
                _loadImplementations(jeImplementations);
            }
            
            if (je.TryGetProperty("scenes", out var jeScenes))
            {
                _loadScenes(jeScenes);
            }

            if (je.TryGetProperty("quests", out var jeQuests))
            {
                _loadQuests(jeQuests);
            }

            if (je.TryGetProperty("layers", out var jeLayers))
            {
                I.Get<LayerCatalogue>().LoadConfig(jeLayers);
            }
        }
    }


    private void _loadAnimationsTo(JsonElement je)
    {
        string pathProbe;
        try
        {
            foreach (var jeRes in je.EnumerateArray())
            {
                string? uriModel = jeRes.GetProperty("modelUrl").GetString();
                if (null == uriModel)
                {
                    throw new InvalidDataException("no modelUrl specified in resource.");
                }
                
                string? uriAnimations = jeRes.GetProperty("animationUrls").GetString();
                if (null == uriAnimations)
                {
                    throw new InvalidDataException("no animationsUrl specified in resource.");
                }

                if (_traceResources) Trace($"LoadAnimationsTo: Added Animation \"{uriModel}\" from {uriModel}.");
                pathProbe = Path.Combine(engine.GlobalSettings.Get("Engine.ResourcePath"), uriModel); 
                if (!File.Exists(pathProbe))
                {
                    Trace($"Warning: animation file for {pathProbe} does not exist.");
                }

                AvailableAnimations.Add($"{uriModel};{uriAnimations}");
                
                var strFileName =
                    ModelAnimationCollectionReader.ModelAnimationCollectionFileName(
                        Path.GetFileName(uriModel),
                        uriAnimations);
                if (_traceResources) Trace($"LoadAnimationsTo: Added Animation {uriModel} with {uriAnimations} at {strFileName}.");
                pathProbe = Path.Combine("generated", strFileName); 
                if (!File.Exists(pathProbe))
                {
                    Trace($"Warning: resource file for {pathProbe} does not exist.");
                }
                _iAssetImpl.AddAssociation(strFileName, pathProbe);
                
            }
        }
        catch (Exception e)
        {
            Trace($"Error loading resource: {e}");
        }

    }

    
    private void _loadResourcesTo(JsonElement je)
    {
        try
        {
            foreach (var jeRes in je.EnumerateArray())
            {
                string? uri = jeRes.GetProperty("uri").GetString();
                if (null == uri)
                {
                    throw new InvalidDataException("no uri specified in resource.");
                }
                

                string? tag = null;
                if (jeRes.TryGetProperty("tag", out var jpTag))
                {
                    tag = jpTag.GetString();
                }
                if (null == tag)
                {
                    int idx = uri.LastIndexOf('/');
                    if (idx != -1 && idx != uri.Length - 1)
                    {
                        tag = uri[(idx + 1)..];
                    }
                    else
                    {
                        tag = uri;
                    }
                }

                if (_traceResources) Trace($"LoadResourcesTo: Added Resource \"{tag}\" from {uri}.");
                string pathProbe = Path.Combine(engine.GlobalSettings.Get("Engine.ResourcePath"), uri); 
                if (!File.Exists(pathProbe))
                {
                    Trace($"Warning: resource file for {pathProbe} does not exist.");
                }
                _iAssetImpl.AddAssociation(tag, uri);
            }
        }
        catch (Exception e)
        {
            Trace($"Error loading resource: {e}");
        }

    }

    
    private IModule _loadRootModule()
    {
        try
        {
            if (_jeRoot.TryGetProperty("modules", out var jeModules))
            {
                if (jeModules.TryGetProperty("root", out var jeRootModule))
                {
                    try
                    {
                        var factory = CreateFactoryMethod(null, jeRootModule);
                        IModule mRoot = factory() as IModule;
                        return mRoot;
                    }
                    catch (Exception e)
                    {
                        ErrorThrow<ArgumentException>($"Exception while instantiating root module: {e}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            ErrorThrow<ArgumentException>($"Unable to find root module definition.");
        }
        

        return null;
    }


    private void _loadDefaults(JsonElement jeDefaults)
    {
        try
        {
            strDefaultLoaderAssembly = jeDefaults.GetProperty("loader").GetProperty("assembly")
                .GetString();
            engine.rom.Loader.SetDefaultLoaderAssembly(strDefaultLoaderAssembly);
        }
        catch (Exception e)
        {
            //
        }
    }
    
    
    private void _loadGameConfigFile(System.IO.Stream stream)
    {
        JsonDocument jdocGame = JsonDocument.Parse(stream, new()
        {
            AllowTrailingCommas = true
        });
        I.Get<Mix>().UpsertFragment("/", jdocGame.RootElement);
        _jeRoot = jdocGame.RootElement;
    }

    
    public void SetAssetLoaderAssociations(IAssetImplementation iasset)
    {
        _iAssetImpl = iasset;
        
        if (_jeRoot.TryGetProperty("resources", out var jeResources))
        {
            if (jeResources.TryGetProperty("list", out var jeList))
            {
                _loadResourcesTo(jeList);
            }
            else
            {
                Error("Warning: No resources/list section found in game.");
            }
        }
        else
        {
            Error("Warning: No resources section found in game.");
        }
        
        if (_jeRoot.TryGetProperty("animations", out var jeAnimations))
        {
            if (jeAnimations.TryGetProperty("list", out var jeList))
            {
                _loadAnimationsTo(jeList);
            }
            else
            {
                Error("Warning: No resources/list section found in game.");
            }
        }
        else
        {
            Error("Warning: No resources section found in game.");
        }
        
        if (_jeRoot.TryGetProperty("textures", out var jeTextures))
        {
            _loadTextures(jeTextures);
        }
        else
        {
            Error("Warning: No textures section found in game.");
        }


    }


    public void InterpretConfig()
    {
        if (_jeRoot.TryGetProperty("defaults", out var jeDefaults))
        {
            _loadDefaults(jeDefaults);
        }
        _loadGameConfig(_jeRoot);
        lock (_lo)
        {
            _isLoaded = true;
        }
        _callWhenLoaded();
    }


    public void StartGame()
    {
        IModule mRoot = _loadRootModule();
        _engine = I.Get<Engine>();
        
        /*
         * Start the root module in the main thread.
         */
        _engine.QueueMainThreadAction(() => { mRoot.ModuleActivate();});
    }


    private void _callSingleWhenLoaded(string path, Action<string, JsonNode?> actWhenLoaded)
    {
        try
        {
            var subTree = _mix.GetTree(path);
            actWhenLoaded(path, subTree);
        }
        catch (Exception e)
        {
            Warning($"Exception partsing configuration subtree {path}: {e}");
        }
    }
    
    
    private void _callWhenLoaded()
    {
        ImmutableList<LoaderSubscription> whenLoadedList;
        lock (_lo)
        {
            whenLoadedList = _whenLoadedList.ToImmutableList();
            _whenLoadedList = null;
        }

        foreach (var subscriber in whenLoadedList)
        {
            _callSingleWhenLoaded(subscriber.Path, subscriber.OnTreeData);
        }
    }
    

    public void WhenLoaded(string path, Action<string, JsonNode?> whenLoaded)
    {
        bool callNow = false;
        lock (_lo)
        {
            if (_isLoaded)
            {
                callNow = true;
            }
            else
            {
                _whenLoadedList.Add(new LoaderSubscription() { Path = path, OnTreeData = whenLoaded });

            }
        }

        if (callNow)
        {
            _callSingleWhenLoaded(path, whenLoaded);
        }
    }
    

    public Loader(System.IO.Stream stream)
    {
        /*
         * This is a special case to register the Mix instance here.
         */
        I.Register<engine.casette.Mix>(() => new engine.casette.Mix());
        _mix = I.Get<Mix>();
        _loadGameConfigFile(stream);
    }
}

