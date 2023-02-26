
using System.Numerics;


namespace engine
{
    public interface IPlatform
    {
        public void SetEngine(engine.Engine engine);
        public IUI CreateUI();
        public void Execute();

        public void CollectRenderData();

        public void GetControllerState(out ControllerState controllerState);

        public void GetMouseMove(out Vector2 vMouseMove);
    }
}
