
using System.Numerics;


namespace engine
{
    public interface IPlatform : System.IDisposable
    {
        public void SetEngine(engine.Engine engine);

       
        public void Execute();

        public bool MouseEnabled { get; set; }
        
        public bool KeyboardEnabled { get; set; }

        /**
         * Hint for the platform keyboard input type.
         * Values: "text", "email", "password", "number"
         */
        public string KeyboardInputType { get; set; }

        /**
         * Collect all data from the ECS to later render a frame.
         * Depending on the rendering queue, the implementation can
         * decide not to collect any data at all.
         */
        public void CollectRenderData(IScene scene);

        public void SetFullscreen(bool isFullscreen);

        public bool IsRunning();

        /**
         * The LAYOUT-DEPENDENT display label for a physical key, e.g. ScanCode.W is "W"
         * on QWERTY and "Z" on AZERTY.
         *
         * A rebinding screen is the only thing that needs this, and it genuinely does:
         * bindings store the POSITION (see engine.inputs.ScanCode), and printing the
         * positional name would tell an AZERTY user to press a key that is not on their
         * keyboard. Display only - never round-trip it and never key anything on it, or
         * a change of layout silently changes what a saved binding means.
         *
         * Default null so a platform that cannot answer does not have to pretend; the
         * caller falls back to the positional name.
         */
        public string? GetKeyDisplayName(engine.inputs.ScanCode scanCode) => null;
    }
}
