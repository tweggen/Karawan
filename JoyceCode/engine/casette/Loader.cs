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

    private List<LoaderSubscription> _whenLoadedList = new();
    
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
        JsonNode? jnCurr = _mix.GetTree(cassettePath);

        if (jnCurr == null)
        {
            return;
        }

        _loadToSerializable(jnCurr, target);
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

    
    private IModule _loadRootModule()
    {
        JsonNode? jnRootModule;
        try
        {
             jnRootModule = _mix.GetTree("/modules/root");
        }
        catch (Exception e)
        {
            ErrorThrow<ArgumentException>($"Unable to find root module definition.");
            return null;
        }
            
        try
        {
            var factory = CreateFactoryMethod(null, jnRootModule);
            IModule mRoot = factory() as IModule;
            return mRoot;
        }
        catch (Exception e)
        {
            ErrorThrow<ArgumentException>($"Exception while instantiating root module: {e}");
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

    
    public void InterpretConfig()
    {
        if (_jeRoot.TryGetProperty("defaults", out var jeDefaults))
        {
            _loadDefaults(jeDefaults);
        }
        
        /*
         * Now that the main game object has been identified, load the global settings.
         */
        engine.GlobalSettings.Instance().StartLoading();
        
        /*
         * With the global settings in place we can trigger loading the actual instances.
         */
        I.Instance.StartLoading();
        
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

