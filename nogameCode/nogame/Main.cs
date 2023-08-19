
using builtin.controllers;
using builtin.map;
using engine;
using nogame.map;

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

    
    private IMapProvider _setupMapProvider()
    {
        lock (_lo)
        {
            _mapProvider = new();
        }
        _mapProvider.AddWorldMapLayer("0100/terrain", new WorldMapTerrainProvider());

        return _mapProvider;
    }
    

    private void _setupImplementations()
    {
        Implementations.Register<IMapProvider>(() => _setupMapProvider());
        Implementations.Register<MapFramebuffer>(() => new MapFramebuffer());
        Implementations.Register<Boom.Jukebox>(() => new Boom.Jukebox());
        Implementations.Register<joyce.ui.Main>(() => new joyce.ui.Main(_e));
        Implementations.Register<builtin.controllers.InputController>(() => new InputController());
    }

    
    private void _registerScenes()
    {
        _e.SceneSequencer.AddSceneFactory("root", () => new nogame.scenes.root.Scene());
        _e.SceneSequencer.AddSceneFactory("logos", () => new nogame.scenes.logos.Scene());
        _e.SceneSequencer.SetMainScene("logos");
    }


    public Main(engine.Engine e)
    {
        _e = e;
    }

    
    public static void Start(engine.Engine e)
    {
        Main main = new(e);

        engine.GlobalSettings.Set("debug.options.flatshading", "false");

        engine.GlobalSettings.Set("world.CreateStreetAnnotations", "false");
        //engine.GlobalSettings.Set("nogame.CreateOSD", "false");
        //engine.GlobalSettings.Set("nogame.CreateMap", "false");
        //engine.GlobalSettings.Set("nogame.CreateMiniMap", "false");
        //engine.GlobalSettings.Set("nogame.CreateTrees", "false");
        //engine.GlobalSettings.Set("world.CreateCubeCharacters", "false");
        //engine.GlobalSettings.Set("world.CreateCar3Characters", "false");
        //engine.GlobalSettings.Set("world.CreateTramCharacters", "false");
        //engine.GlobalSettings.Set("world.CreateStreets", "false");
        //engine.GlobalSettings.Set("world.CreateClusterQuarters", "false");
        
        
        main._setupImplementations();
        main._registerScenes();

        Implementations.Get<Boom.ISoundAPI>().SetupDone();

    }
}
