
using System.Numerics;


namespace engine
{
    public interface IPlatform
    {
        public void SetEngine(engine.Engine engine);

        
        public void Execute();

        /**
         * Collect all data from the ECS to later render a frame.
         * Depending on the rendering queue, the implementation can
         * decide not to collect any data at all.
         */
        public void CollectRenderData();

        public void GetControllerState(out ControllerState controllerState);

        public void GetMouseMove(out Vector2 vMouseMove);
        public void Sleep(double dt);

        public bool IsRunning();
    }
}
