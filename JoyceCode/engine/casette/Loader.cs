using System;
using System.IO;
using System.Text.Json;
using builtin.map;
using engine.world;
using static engine.Logger;

namespace engine.casette;

public class Loader
{
    private JsonElement _jeRoot;
    private string strDefaultLoaderAssembly = "";
    
    static public void SetJsonElement(in JsonElement je, Action<object> action)
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
    

    public void LoadMapProviders(JsonElement jeMapProviders)
    {
        var mapProvider = I.Get<IMapProvider>();
        try
        {
            foreach (var pair in jeMapProviders.EnumerateObject())
            {
                try
                {
                    var className = pair.Value.GetProperty("className").GetString();
                    if (String.IsNullOrWhiteSpace(className))
                    {
                        Warning($"Encountered null classname for {pair}.");
                    }
                    try
                    {
                        IWorldMapProvider wmp = engine.rom.Loader.LoadClass(strDefaultLoaderAssembly, className) as IWorldMapProvider;
                        mapProvider.AddWorldMapLayer(pair.Name, wmp);
                    }
                    catch (Exception e)
                    {
                        Warning($"Unable to load world map layer {pair.Name}: {e}");
                    }
                }
                catch (Exception e)
                {
                    Warning($"Error setting map provider {pair.Name}: {e}");
                }
            }
        }
        catch (Exception e)
        {
            Warning($"Error reading map provider: {e}");
        }

    }


    public void LoadImplementations(JsonElement jeImplementations)
    {
        try
        {
            foreach (var pair in jeImplementations.EnumerateObject())
            {
                string interfaceName = pair.Name;
                string implementationName = null;
                if (pair.Value.ValueKind == JsonValueKind.Object)
                {
                    implementationName = pair.Value.GetProperty("className").GetString();
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
                        I.Instance.RegisterFactory(type, () => { return Activator.CreateInstance(type); });
                    }
                    else
                    {
                        I.Instance.RegisterFactory(type, () => { return engine.rom.Loader.LoadClass(strDefaultLoaderAssembly,implementationName); });
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


    public void LoadBuildingOperators(JsonElement je)
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


    public void LoadPopulatingOperators(JsonElement je)
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


    public void LoadMetaGen(JsonElement je)
    {
        try
        {
            if (je.TryGetProperty("buildingOperators", out var jeBuildingOperators))
            {
                LoadBuildingOperators(jeBuildingOperators);
            }

            if (je.TryGetProperty("populatingOperators", out var jePopulatingOperators))
            {
                LoadPopulatingOperators(jePopulatingOperators);
            }
        }
        catch (Exception e)
        {
            Warning($"Unable to setup metagen: {e}");
        }
    }

   
    private void LoadGlobalSettings(JsonElement jeGlobalSettings)
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


    private void LoadProperties(JsonElement jeProperties)
    {
        try
        {
            foreach (var pair in jeProperties.EnumerateObject())
            {
                try
                {
                    SetJsonElement(pair.Value, o => engine.Props.Set(pair.Name, o));
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


    public void LoadQuests(JsonElement jeQuests)
    {
        try
        {
            foreach (var pair in jeQuests.EnumerateObject())
            {
                try
                {
                    string questName = pair.Name;
                    string implementationName = null;
                    if (pair.Value.ValueKind == JsonValueKind.String)
                    {
                        implementationName = pair.Value.GetProperty("className").GetString();
                    }
                    if (String.IsNullOrWhiteSpace(implementationName))
                    {
                        implementationName = questName;
                    }
                    
                    I.Get<engine.quest.Manager>().RegisterFactory(questName, (_) => engine.rom.Loader.LoadClass(strDefaultLoaderAssembly, implementationName) as engine.quest.IQuest);
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
    
    
    public void LoadScenes(JsonElement je)
    {
        var sceneSequencer = I.Get<SceneSequencer>();
        if (je.TryGetProperty("catalogue", out var jeCatalogue))
        {
            sceneSequencer.AddFrom(jeCatalogue);
        }
        sceneSequencer.SetMainScene(je.GetProperty("startup").GetString());
    }


    public void LoadGameConfig(JsonElement je)
    {
        if (je.TryGetProperty("globalSettings", out var jeGlobalSettings))
        {
            LoadGlobalSettings(jeGlobalSettings);
        }

        if (je.TryGetProperty("properties", out var jeProperties))
        {
            LoadProperties(jeProperties);
        }

        if (je.TryGetProperty("implementations", out var jeImplementations))
        {
            LoadImplementations(jeImplementations);
        }

        if (je.TryGetProperty("mapProviders", out var jeMapProviders))
        {
            LoadMapProviders(jeMapProviders);
        }

        if (je.TryGetProperty("metaGen", out var jeMetaGen))
        {
            LoadMetaGen(jeMetaGen);
        }

        if (je.TryGetProperty("scenes", out var jeScenes))
        {
            LoadScenes(jeScenes);
        }

        if (je.TryGetProperty("quests", out var jeQuests))
        {
            LoadQuests(jeQuests);
        }
    }


    public void LoadResourcesTo(IAssetImplementation iasset, JsonElement je)
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

                Trace($"LoadResourcesTo: Added Resource \"{tag}\" from {uri}.");
                string pathProbe = Path.Combine(engine.GlobalSettings.Get("Engine.ResourcePath"), uri); 
                if (!File.Exists(pathProbe))
                {
                    Trace($"Warning: resource file for {pathProbe} does not exist.");
                }
                iasset.AddAssociation(tag, uri);
            }
        }
        catch (Exception e)
        {
            Trace($"Error loading resource: {e}");
        }

    }

    
    public IModule LoadRootModule()
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
        _jeRoot = jdocGame.RootElement;
    }

    
    public void SetAssetLoaderAssociations(IAssetImplementation iasset)
    {
        if (_jeRoot.TryGetProperty("resources", out var jeResources))
        {
            if (jeResources.TryGetProperty("list", out var jeList))
            {
                LoadResourcesTo(iasset, jeList);
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
    }

    public void InterpretConfig()
    {
        if (_jeRoot.TryGetProperty("defaults", out var jeDefaults))
        {
            _loadDefaults(jeDefaults);
        }
        LoadGameConfig(_jeRoot);
    }


    public void StartGame()
    {
        var e = I.Get<Engine>();
        IModule mRoot = LoadRootModule();
        
        /*
         * Start the root module in the main thread.
         */
        e.QueueMainThreadAction(() => { mRoot.ModuleActivate();});
    }
    

    public Loader(System.IO.Stream stream)
    {
        _loadGameConfigFile(stream);
    }
    

    static public void LoadStartGame(string jsonPath)
    {
        /*
         * Load the game config
         */
        var loader = new engine.casette.Loader(engine.Assets.Open("nogame.json"));
        loader.InterpretConfig();
    }
}