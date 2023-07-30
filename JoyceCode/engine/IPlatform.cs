
using System.Numerics;


namespace engine
{
    public interface IPlatform : System.IDisposable
    {
        public void SetEngine(engine.Engine engine);

        
        public void Execute();

        /**
         * Collect all data from the ECS to later render a frame.
         * Depending on the rendering queue, the implementation can
         * decide not to collect any data at all.
         */
        public void CollectRenderData(IScene scene);

        public void GetControllerState(out ControllerState controllerState);

        public void GetMouseMove(out Vector2 vMouseMove);
        public void Sleep(double dt);

        public void SetFullscreen(bool isFullscreen);
        
        public bool IsRunning();
    }
}
