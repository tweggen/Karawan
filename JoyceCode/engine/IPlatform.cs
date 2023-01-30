
namespace engine
{
    public interface IPlatform
    {
        public void SetEngine(engine.Engine engine);
        public IUI CreateUI();
        public void Execute();
        public void Render3D();

        public void GetControllerState(out ControllerState controllerState);
    }
}
