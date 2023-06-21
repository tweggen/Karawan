
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
    

    public void SetupDependencies()
    {
        Implementations.Register<IMapProvider>(() => _setupMapProvider());
    }

    public void LoadScenes()
    {
        _e.SceneSequencer.AddSceneFactory("root", () => new nogame.RootScene());
        _e.SceneSequencer.AddSceneFactory("logos", () => new nogame.LogosScene());
        _e.SceneSequencer.SetMainScene("logos");
    }


    public Main(engine.Engine e)
    {
        _e = e;
    }

    
    public static void Start(engine.Engine e)
    {
        Main main = new(e);

        main.SetupDependencies();
        main.LoadScenes();
    }
}
