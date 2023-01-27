

namespace engine.xray
{
    public class RootScene : engine.IScene
    {
        private engine.Engine _engine;
        private engine.IUI _ui;

        public void SceneActivate(Engine engine)
        {
            _engine = engine;

            _ui = engine.CreateUI();

            _engine.AddScene(10, this);
        }

        public void SceneDeactivate()
        {
            _engine.RemoveScene(this);
        }

        public void SceneOnLogicalFrame(float dt)
        {
        }

        public void SceneOnPhysicalFrame(float dt)
        {
            _ui.Render();
        }

        public RootScene()
        {
        }

    }
}
