
#if false
namespace engine.xray
{
    public class RootScene : engine.IScene
    {
        private engine.Engine _engine;
        private engine.IUI _ui;

        public void SceneActivate(Engine engine)
        {
            _engine = engine;
#if false
            _ui = engine.CreateUI();
            string strTestUI = @"
<?xml version=\""1.0\""?>
<flex>
    <button id='btnFinish'>Finish</button>
    <button id='btnAbort'>Abort</button>
</flex>  
";
            var _uiTest = _ui.CreateUI(strTestUI);
#endif
            _engine.SceneSequencer.AddScene(10, this);
        }

        public void SceneDeactivate()
        {
            _engine.SceneSequencer.RemoveScene(this);
        }

        public void SceneOnLogicalFrame(float dt)
        {
        }

        public RootScene()
        {
        }

    }
}
#endif