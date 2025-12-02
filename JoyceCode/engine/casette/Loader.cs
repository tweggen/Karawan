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
    

    private void _loadToSerializable(JsonElement jeCurr, object target)
    {
        _engine = I.Get<Engine>();

        var iSerializable = target as ISerializable;
        if (null == iSerializable)
        {
            Error(
                $"Trying to load config to object of type {target.GetType().Name} failed, not ISerializable.");
            return;
        }

        try
        {
            _engine.Run(iSerializable.SetupFrom(jeCurr));
        }
        catch (Exception e)
        {
            Error($"Unable to load from config: {e}.");
        }
    }


    private void _loadToSerializable(string cassettePath, object target)
    {
        String[] pathParts = cassettePath.Split('.');
        var jeCurr = _jeRoot;
        bool isInvalid = false;
        foreach (var part in pathParts)
        {
            if (!jeCurr.TryGetProperty(part, out var jeNextPart))
            {
                isInvalid = true;
                Error($"Unable to resolve config path {cassettePath}: unable to find {part}.");
                break;
            }

            jeCurr = jeNextPart;
        }

        if (isInvalid)
        {
            return;
        }

        _loadToSerializable(jeCurr, target);
    }


    private void _loadImplementations(JsonElement jeImplementations)
    {
        if (engine.GlobalSettings.Get("joyce.CompileMode") == "true")
        {
            ErrorThrow<InvalidOperationException>("I should not have been called.");
            return;
        }
        try
        {
            foreach (var pair in jeImplementations.EnumerateObject())
            {
                string interfaceName = pair.Name;
                string implementationName = null;
                Action<object> setupProperties = null;
                string cassettePath = null;
                JsonElement jeConfig = default;
                bool haveConfig = false;
                
                if (pair.Value.ValueKind == JsonValueKind.Object)
                {
                    if (pair.Value.TryGetProperty("config", out jeConfig)
                        && jeConfig.ValueKind == JsonValueKind.Object)
                    {
                        haveConfig = true;
                    }
                    
                    if (pair.Value.TryGetProperty("className", out var jeClassName)
                        && jeClassName.ValueKind == JsonValueKind.String)
                    {
                        implementationName = pair.Value.GetProperty("className").GetString();
                    }

                    if (pair.Value.TryGetProperty("cassettePath", out var jeCassettePath)
                        && jeCassettePath.ValueKind == JsonValueKind.String)
                    {
                        cassettePath = pair.Value.GetProperty("cassettePath").GetString();
                    }

                    if (pair.Value.TryGetProperty("properties", out var jeProperties) 
                        && jeProperties.ValueKind == JsonValueKind.Object)
                    {
                        setupProperties = (object obj) =>
                        {
                            foreach (var pair in jeProperties.EnumerateObject())
                            {
                                PropertyInfo prop = obj.GetType().GetProperty(
                                    pair.Name, BindingFlags.Public | BindingFlags.Instance);
                                if(null != prop && prop.CanWrite)
                                {
                                    switch (pair.Value.ValueKind)
                                    {
                                        case JsonValueKind.String:
                                            prop.SetValue(obj, pair.Value.GetString(), null);
                                            break;
                                        case JsonValueKind.Number:
                                            prop.SetValue(obj, (float) pair.Value.GetDouble(), null);
                                            break;
                                        case JsonValueKind.Object:
                                        {
                                            SortedDictionary<string, string> dict = new();
                                            foreach (var kvpDict in pair.Value.EnumerateObject())
                                            {
                                                
                                                dict[kvpDict.Name] = kvpDict.Value.GetString();
                                            }
                                            prop.SetValue(obj, dict, null);
                                        }
                                            break;
                                    }
                                }
                            }
                        };
                    }
                }
                if (String.IsNullOrWhiteSpace(implementationName))
                {
                    /*
                     * Use the key as the className
                     */
                    implementationName = interfaceName;
                }

                try
                {
                    Type type = engine.rom.Loader.LoadType(strDefaultLoaderAssembly, interfaceName);
                    if (interfaceName == implementationName)
                    {
                        I.Instance.RegisterFactory(type, () => {
                            var i = Activator.CreateInstance(type);
                            if (null != setupProperties) setupProperties(i);
                            if (haveConfig) _loadToSerializable(jeConfig, i);
                            if (null != cassettePath) _loadToSerializable(cassettePath, i);
                            return i;
                        });
                    }
                    else
                    {
                        I.Instance.RegisterFactory(type, () =>
                        {
                            var i = engine.rom.Loader.LoadClass(strDefaultLoaderAssembly, implementationName);
                            if (null != setupProperties) setupProperties(i);
                            if (haveConfig) _loadToSerializable(jeConfig, i);
                            if (null != cassettePath) _loadToSerializable(cassettePath, i);
                            return i;
                        });
                    }
                }
                catch (Exception e)
                {
                    Warning($"Unable to load implementation type {pair.Name}: {e}");
                }
            }
        }
        catch (Exception e)
        {
            Warning($"Error reading implementations: {e}");
        }
    }


    private ExecDesc _loadExecDesc(JsonElement je)
    {
        engine.meta.ExecDesc.ExecMode execMode = ExecDesc.ExecMode.Task;
        if (je.TryGetProperty("mode", out var jeMode))
        {
            switch (jeMode.ValueKind)
            {
                case JsonValueKind.String:
                    var str = jeMode.GetString();
                    if (str != null)
                    {
                        switch (str)
                        {
                            case "constant":
                                execMode = ExecDesc.ExecMode.Constant;
                                break;
                            default:
                            case "task":
                                execMode = ExecDesc.ExecMode.Task;
                                break;
                            case "parallel":
                                execMode = ExecDesc.ExecMode.Parallel;
                                break;
                            case "applyParallel":
                                execMode = ExecDesc.ExecMode.ApplyParallel;
                                break;
                            case "sequence":
                                execMode = ExecDesc.ExecMode.Sequence;
                                break;
                        }
                    }
                    break;
                case JsonValueKind.Number:
                    execMode = (ExecDesc.ExecMode) jeMode.GetInt32();
                    break;
                default:
                    break;
            }
        }

        string comment = null;
        if (je.TryGetProperty("comment", out var jeComment))
        {
            comment = jeComment.GetString();
        }

        string configCondition = null;
        if (je.TryGetProperty("configCondition", out var jeConfigCondition))
        {
            configCondition = jeConfigCondition.GetString();
        }

        string selector = null;
        if (je.TryGetProperty("selector", out var jeSelector))
        {
            selector = jeSelector.GetString();
        }

        string target = null;
        if (je.TryGetProperty("target", out var jeTarget))
        {
            target = jeTarget.GetString();
        }

        string implementation = null;
        if (je.TryGetProperty("implementation", out var jeImplementation))
        {
            implementation = jeImplementation.GetString();
        }

        List<ExecDesc> children = new List<ExecDesc>();
        if (je.TryGetProperty("children", out var jeChildren))
        {
            foreach (var jeChild in jeChildren.EnumerateArray())
            {
                children.Add(_loadExecDesc(jeChild));
            }
        }

        if (children.Count == 0)
        {
            children = null;
        }
        
        return new ExecDesc()
        {
            Mode = execMode,
            Comment = comment,
            ConfigCondition = configCondition,
            Selector = selector,
            Target = target,
            Children = children,
            Implementation = implementation
        };
    }
    

    private ExecDesc _loadFragmentOperators(JsonElement je)
    {
        try
        {
            var edRoot = _loadExecDesc(je);
            I.Get<MetaGen>().EdRoot = edRoot;
        }
        catch (Exception e)
        {
            Warning($"Error reading fragment operators: {e}.");
        }

        return new ExecDesc();
    }


    private void _loadBuildingOperators(JsonElement je)
    {
        try
        {
            foreach (var jeOp in je.EnumerateArray())
            {
                string className = jeOp.GetProperty("className").GetString();
                try
                {
                    IWorldOperator wop = engine.rom.Loader.LoadClass(strDefaultLoaderAssembly, className) as IWorldOperator;
                    I.Get<MetaGen>().WorldBuildingOperators.Add(wop);
                }
                catch (Exception e)
                {
                    Warning($"Unable to instantiate world building operator {className}: {e}");
                }
            }
        }
        catch (Exception e)
        {
            Warning($"Error reading implementations: {e}");
        }
    }


    private void _loadClusterOperators(JsonElement je)
    {
        try
        {
            foreach (var jeOp in je.EnumerateArray())
            {
                string className = jeOp.GetProperty("className").GetString();
                try
                {
                    IClusterOperator cop = engine.rom.Loader.LoadClass(strDefaultLoaderAssembly, className) as IClusterOperator;
                    I.Get<MetaGen>().ClusterOperators.Add(cop);
                }
                catch (Exception e)
                {
                    Warning($"Unable to instantiate world building operator {className}: {e}");
                }
            }
        }
        catch (Exception e)
        {
            Warning($"Error reading implementations: {e}");
        }
    }


    private void _loadPopulatingOperators(JsonElement je)
    {
        try
        {
            foreach (var jeOp in je.EnumerateArray())
            {
                string className = jeOp.GetProperty("className").GetString();
                try
                {
                    IWorldOperator wop = engine.rom.Loader.LoadClass(strDefaultLoaderAssembly, className) as IWorldOperator;
                    I.Get<MetaGen>().WorldPopulatingOperators.Add(wop);
                }
                catch (Exception e)
                {
                    Warning($"Unable to instantiate world populating operator {className}: {e}");
                }
            }
        }
        catch (Exception e)
        {
            Warning($"Error reading implementations: {e}");
        }
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
    

    private void _loadMetaGen(JsonElement je)
    {
        try
        {
            if (je.TryGetProperty("buildingOperators", out var jeBuildingOperators))
            {
                _loadBuildingOperators(jeBuildingOperators);
            }

            if (je.TryGetProperty("clusterOperators", out var jeClusterOperators))
            {
                _loadClusterOperators(jeClusterOperators);
            }

            if (je.TryGetProperty("populatingOperators", out var jePopulatingOperators))
            {
                _loadPopulatingOperators(jePopulatingOperators);
            }
            
            if (je.TryGetProperty("fragmentOperators", out var jeFragmentOperators))
            {
                _loadFragmentOperators(jeFragmentOperators);
            }
        }
        catch (Exception e)
        {
            Warning($"Unable to setup metagen: {e}");
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
    private Func<object> _createFactoryMethod(string key, JsonElement jeValue)
    {
        string strClassName = default;
        string strMethodName = default;
        CreationType creationType = CreationType.Undefined;
        
        if (jeValue.ValueKind == JsonValueKind.Object)
        {
            if(jeValue.TryGetProperty("implementation", out var jeImplementation))
            {
                string? strImplementation = jeImplementation.GetString();
                if (!String.IsNullOrWhiteSpace(strImplementation))
                {
                    strMethodName = strImplementation;
                    creationType = CreationType.FactoryMethod;
                }
            }
            
            if(jeValue.TryGetProperty("className", out var jeClassName))
            {
                string? strClassNameCand = jeClassName.GetString();
                if (!String.IsNullOrWhiteSpace(strClassNameCand))
                {
                    strMethodName = strClassNameCand;
                    creationType = CreationType.Constructor;
                }
            }
        }
        
        if (creationType == CreationType.Undefined)
        {
            strClassName = key;
            creationType = CreationType.Constructor;
        }

        switch (creationType)
        {
            default:
            case CreationType.Undefined:
                ErrorThrow<ArgumentException>($"Invalid factory definition for {jeValue.ToString()}");
                return () => 
                    null;
            
            case CreationType.Constructor:
                return () => engine.rom.Loader.LoadClass(
                    strDefaultLoaderAssembly, strClassName);
            
            case CreationType.FactoryMethod:
                return () =>
                {
                    int lastDot = strMethodName.LastIndexOf('.');
                    if (-1 == lastDot)
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
                            if (t != null)
                            {
                                break;
                            }
                        }
                    }
                    if (null == t)
                    {
                        ErrorThrow($"Class \"{className}\" not found.",
                            m => new ArgumentException(m));
                    }

                    var methodInfo = t.GetMethod(methodName);
                    if (null == methodInfo)
                    {
                        ErrorThrow(
                            $"Method \"{methodName}\"(IDictionary<string, object>) not found in class \"{className}\".",
                            m => new ArgumentException(m));
                    }
        
                    /*
                     * Finally, create the instance of the object we shall call.
                     */
                    return methodInfo.Invoke(null, new object[] {});
                };
        }
    }


    private void _loadQuests(JsonElement jeQuests)
    {
        try
        {
            foreach (var pair in jeQuests.EnumerateObject())
            {
                try
                {
                    string questName = pair.Name;
                    I.Get<engine.quest.Manager>().RegisterFactory(questName, _ => _createFactoryMethod(pair.Name, pair.Value)() as engine.quest.IQuest);
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
    
    
    private void _loadScenes(JsonElement je)
    {
        var sceneSequencer = I.Get<SceneSequencer>();
        if (je.TryGetProperty("catalogue", out var jeCatalogue))
        {
            sceneSequencer.AddFrom(jeCatalogue);
        }
        sceneSequencer.SetMainScene(je.GetProperty("startup").GetString());
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

            if (je.TryGetProperty("metaGen", out var jeMetaGen))
            {
                _loadMetaGen(jeMetaGen);
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
                    if (jeRootModule.TryGetProperty("className", out var jeClassName))
                    {
                        IModule mRoot = engine.rom.Loader.LoadClass(strDefaultLoaderAssembly, jeClassName.GetString()) as IModule;
                        return mRoot;
                    }
                }
            }
        }
        catch (Exception e)
        {
            ErrorThrow<ArgumentException>($"Unable to find root module at modules/root in config.");
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
    

    #if false
    static public void LoadStartGame(string jsonPath)
    {
        /*
         * Load the game config
         */
        I.Register<engine.casette.Mix>(() => new engine.casette.Mix());
        var loader = new engine.casette.Loader(engine.Assets.Open("nogame.json"));
        loader.InterpretConfig();
    }
    #endif
}

