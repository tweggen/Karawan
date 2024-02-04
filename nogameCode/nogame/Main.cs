
using System;
using System.Text.Json;
using System.Timers;
using builtin.controllers;
using builtin.map;
using engine;
using engine.world;
using Newtonsoft.Json.Linq;
using nogame.map;
using static engine.Logger;

namespace nogame;

/**
 * This is the game implementation main class.
 * It should
 * - setup te scenes in a way that a start set is or will be set
 * - setup all dependencies
 */
public class Main
{
    private object _lo = new ();
    
    private engine.Engine _e;
    
    private System.Timers.Timer _saveTimer;

    private JsonDocument _jdocGame;
    private JsonElement _jsonRoot;

    private void _onSaveTimer(object sender, ElapsedEventArgs e)
    {
        I.Get<DBStorage>().SaveGameState(I.Get<GameState>());
    }
    
    
    private void _setupMapProviders(JsonElement je)
    {
        var mapProvider = I.Get<IMapProvider>();
        try
        {
            var jeMapProviders = je.GetProperty("mapProviders");
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
                        IWorldMapProvider wmp = engine.Engine.LoadClass("nogame.dll", className) as IWorldMapProvider;
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
    

    private void _setupImplementations(JsonElement je)
    {
        try
        {
            var jeImplementations = je.GetProperty("implementations");
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
                    Type type = engine.Engine.LoadType("nogame.dll", interfaceName);
                    if (interfaceName == implementationName)
                    {
                        I.Instance.RegisterFactory(type, () => { return Activator.CreateInstance(type); });
                    }
                    else
                    {
                        I.Instance.RegisterFactory(type, () => { return engine.Engine.LoadClass("nogame.dll",implementationName); });
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


    private void _setupBuildingOperators(JsonElement je)
    {
        try
        {
            foreach (var jeOp in je.EnumerateArray())
            {
                string className = jeOp.GetProperty("className").GetString();
                try
                {
                    IWorldOperator wop = engine.Engine.LoadClass("nogame.dll", className) as IWorldOperator;
                    MetaGen.Instance().WorldBuildingOperatorAdd(wop);
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


    private void _setupPopulatingOperators(JsonElement je)
    {
        try
        {
            foreach (var jeOp in je.EnumerateArray())
            {
                string className = jeOp.GetProperty("className").GetString();
                try
                {
                    IWorldOperator wop = engine.Engine.LoadClass("nogame.dll", className) as IWorldOperator;
                    MetaGen.Instance().WorldPopulatingOperatorAdd(wop);
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


    private void _setupMetaGen(JsonElement je)
    {
        try
        {
            _setupBuildingOperators(je.GetProperty("metaGen").GetProperty("buildingOperators"));
            _setupPopulatingOperators(je.GetProperty("metaGen").GetProperty("populatingOperators"));
        }
        catch (Exception e)
        {
            Warning($"Unable to setup metagen: {e}");
        }
    }

   
    private SceneSequencer _sceneSequencer;
    
    private void _registerScenes(JsonElement je)
    {
        _sceneSequencer = I.Get<SceneSequencer>();
        _sceneSequencer.AddFrom(je);
    }

    private void _startScenes()
    {
        try
        {
            _sceneSequencer.SetMainScene(_jsonRoot.GetProperty("startScene").GetProperty("name").GetString());
        }
        catch (Exception e)
        {
            Warning($"Unable to set start scene from json.");
        }
    }


    private void _startAutoSave()
    {
        _saveTimer = new System.Timers.Timer(60000);
        // Hook up the Elapsed event for the timer. 
        _saveTimer.Elapsed += _onSaveTimer;
        _saveTimer.AutoReset = true;
        _saveTimer.Enabled = true;
    }


    private void _readGlobalSettings(JsonElement je)
    {
        try
        {
            var jeGlobalSettings = je.GetProperty("globalSettings");
            foreach (var pair in jeGlobalSettings.EnumerateObject())
            {
                try
                {
                    engine.GlobalSettings.Set(pair.Name, pair.Value.GetString());
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


    private void SetJsonElement(in JsonElement je, Action<object> action)
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
    

    private void _readProperties(JsonElement je)
    {
        try
        {
            var jeProperties = je.GetProperty("properties");
            foreach (var pair in jeProperties.EnumerateObject())
            {
                try
                {
                    SetJsonElement(je, o => engine.Props.Set(pair.Name, o));
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


    public Main(engine.Engine e)
    {
        _e = e;

        _jdocGame = JsonDocument.Parse(engine.Assets.Open("nogame.json"), new()
        {
            AllowTrailingCommas = true
        });
        _jsonRoot = _jdocGame.RootElement;
        
        _readGlobalSettings(_jsonRoot);
        _readProperties(_jsonRoot);
        
        _setupImplementations(_jsonRoot);
        _setupMapProviders(_jsonRoot);
        _setupMetaGen(_jsonRoot);
        _registerScenes(_jsonRoot);
    }
    
    
    public static void Start(engine.Engine e)
    {
        Main main = new(e);
        
        /*
         * Register the quests
         */
        // TXWTODO: Can't we do that in json?
        I.Get<engine.quest.Manager>().RegisterFactory("nogame.quests.VisitAgentTwelve.Quest",
            (_) => new nogame.quests.VisitAgentTwelve.Quest());
        
        main._startScenes();

        {
            bool haveGameState = I.Get<DBStorage>().LoadGameState(out GameState gameState);
            if (false == haveGameState)
            {
                gameState = new GameState();
                I.Get<DBStorage>().SaveGameState(gameState);
            }
            else
            {
                if (!gameState.IsValid())
                {
                    gameState.Fix();
                }
            }
            /*
             * Global Data structures
             */
            I.Register<GameState>(() => gameState);
        }

        // TXWTODO: How can we remove this call?
        I.Get<Boom.ISoundAPI>().SetupDone();

        main._startAutoSave();

    }
}
