
using System.Timers;
using builtin.controllers;
using builtin.map;
using engine;
using engine.world;
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
    
    private DefaultMapProvider _mapProvider;

    private System.Timers.Timer _saveTimer;

    private void _onSaveTimer(object sender, ElapsedEventArgs e)
    {
        I.Get<DBStorage>().SaveGameState(I.Get<GameState>());
    }
    
    
    private IMapProvider _setupMapProvider()
    {
        lock (_lo)
        {
            _mapProvider = new();
        }
        _mapProvider.AddWorldMapLayer("0100/terrain", new WorldMapTerrainProvider());
        _mapProvider.AddWorldMapLayer("0110/transport", new WorldMapIntercityProvider());
        _mapProvider.AddWorldMapLayer("0120/cluster", new WorldMapClusterProvider());

        return _mapProvider;
    }
    

    private void _setupImplementations()
    {
        I.Register<DBStorage>(() => new DBStorage());
        I.Register<engine.streets.ClusterStorage>(() => new engine.streets.ClusterStorage());

        I.Register<IMapProvider>(() => _setupMapProvider());
        I.Register<MapFramebuffer>(() => new MapFramebuffer() {Engine = _e });
        I.Register<Boom.Jukebox>(() => new Boom.Jukebox());
        I.Register<joyce.ui.Main>(() => new joyce.ui.Main(_e));
        I.Register<builtin.controllers.InputController>(() => new InputController());
        I.Register<SetupMetaGen>(() => new SetupMetaGen());
        I.Register<nogame.intercity.Network>(() => new nogame.intercity.Network());
    }


    private void _setupMetaGen()
    {
        MetaGen.Instance().WorldBuildingOperatorAdd(new nogame.characters.intercity.GenerateTracksOperator(_e));
        MetaGen.Instance().WorldPopulatingOperatorAdd(new nogame.characters.intercity.GenerateCharacterOperator(_e));
    }

    
    private void _registerScenes()
    {
        _e.SceneSequencer.AddSceneFactory("root", () => new nogame.scenes.root.Scene());
        _e.SceneSequencer.AddSceneFactory("logos", () => new nogame.scenes.logos.Scene());
        //_e.SceneSequencer.AddSceneFactory("tunestreets", () => new builtin.tunestreets.Scene());
        //_e.SceneSequencer.SetMainScene("tunestreets");
    }

    private void _startScenes()
    {
        _e.SceneSequencer.SetMainScene("logos");
    }


    private void _startAutoSave()
    {
        _saveTimer = new System.Timers.Timer(60000);
        // Hook up the Elapsed event for the timer. 
        _saveTimer.Elapsed += _onSaveTimer;
        _saveTimer.AutoReset = true;
        _saveTimer.Enabled = true;
    }


    public Main(engine.Engine e)
    {
        _e = e;
    }
    
    
    public static void Start(engine.Engine e)
    {
        Main main = new(e);

        engine.GlobalSettings.Set("nogame.CreateOSD", "true");
        engine.GlobalSettings.Set("nogame.framebuffer.resolution", "368x207");
        engine.GlobalSettings.Set("nogame.CreateOSD", "true");
        engine.GlobalSettings.Set("nogame.CreateMap", "true");
        engine.GlobalSettings.Set("nogame.CreateMiniMap", "true");
        engine.GlobalSettings.Set("nogame.LogosScene.PlayTitleMusic", "true");

        engine.Props.Set("world.CreateStreetAnnotations", false);
        engine.Props.Set("nogame.CreateTrees", true);
        engine.Props.Set("nogame.CreateHouses", true);
        engine.Props.Set("world.CreateCubeCharacters", true);
        engine.Props.Set("world.CreateCar3Characters", true);
        engine.Props.Set("world.CreateTramCharacters", true);
        engine.Props.Set("world.CreateStreets", true);
        engine.Props.Set("world.CreateClusterQuarters", true);

        engine.Props.Set("nogame.CreateHouses", "true");
        engine.Props.Set("nogame.CreateTrees", "true");


        engine.Props.Set("debug.options.flatshading", false);
        engine.Props.Set("nogame.characters.cube.maxDistance", 400f);
        engine.Props.Set("nogame.characters.car3.maxDistance", 800f);
        engine.Props.Set("nogame.characters.tram.maxDistance", 1600f);
        
        main._setupImplementations();
        main._setupMetaGen();
        main._registerScenes();
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

        I.Get<Boom.ISoundAPI>().SetupDone();

        main._startAutoSave();

    }
}
