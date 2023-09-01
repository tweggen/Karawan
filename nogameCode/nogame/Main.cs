
using builtin.controllers;
using builtin.map;
using engine;
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
        Implementations.Register<SetupMetaGen>(() => new SetupMetaGen());
    }

    
    private void _registerScenes()
    {
        _e.SceneSequencer.AddSceneFactory("root", () => new nogame.scenes.root.Scene());
        _e.SceneSequencer.AddSceneFactory("logos", () => new nogame.scenes.logos.Scene());
        //_e.SceneSequencer.AddSceneFactory("tunestreets", () => new builtin.tunestreets.Scene());
        //_e.SceneSequencer.SetMainScene("tunestreets");
        _e.SceneSequencer.SetMainScene("logos");
    }


    public Main(engine.Engine e)
    {
        _e = e;
    }
    
    
    public static void Start(engine.Engine e)
    {
        Main main = new(e);

        engine.GlobalSettings.Set("nogame.CreateOSD", "true");
        engine.GlobalSettings.Set("nogame.CreateMap", "true");
        engine.GlobalSettings.Set("nogame.CreateMiniMap", "true");

        engine.Props.Set("world.CreateStreetAnnotations", false);
        engine.Props.Set("nogame.CreateTrees", true);
        engine.Props.Set("nogame.CreateHouses", true);
        engine.Props.Set("world.CreateCubeCharacters", true);
        engine.Props.Set("world.CreateCar3Characters", true);
        engine.Props.Set("world.CreateTramCharacters", true);
        engine.Props.Set("world.CreateStreets", true);
        engine.Props.Set("world.CreateClusterQuarters", true);

        engine.Props.Set("debug.options.flatshading", false);
        engine.Props.Set("nogame.characters.cube.maxDistance", 400f);
        engine.Props.Set("nogame.characters.car3.maxDistance", 800f);
        engine.Props.Set("nogame.characters.tram.maxDistance", 1600f);

        
        main._setupImplementations();
        main._registerScenes();

        Implementations.Get<Boom.ISoundAPI>().SetupDone();

    }
}
