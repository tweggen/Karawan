
using builtin.map;
using engine;

namespace nogame;

public class Main
{
    private engine.Engine _e;

    public void SetupDependencies()
    {
        Implementations.Register<IMapProvider>(() => new DefaultMapProvider());
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
