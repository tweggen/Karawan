using System.Numerics;
using Android.App;
using Android.Content.PM;
using Android.OS;
using System.Runtime.InteropServices;
using Splash.Silk;

using Android.Content.Res;
using Android.Views;
using engine;
using AndroidX.Core.App;
using engine.news;
using Java.Lang;
using Org.Libsdl.App;
using View = Android.Views.View;

namespace Wuka
{
    [Activity(
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density,
        ScreenOrientation = ScreenOrientation.Landscape,
        Theme = "@style/SiliconDesert.Theme"
    )]
    public class GameActivity : SDLActivity, ActivityCompat.IOnRequestPermissionsResultCallback
    {
        private object _lo = new();
        private bool _triggeredGame = false;

        internal static AssetManager AssetManager;

        private Sdl3WindowBackend _backend;
        private engine.Engine _engine;


        protected override void OnStop()
        {
            try
            {
                /*
                 * Try to save a backup copy
                 */
                I.Get<engine.Saver>()?.Save("OnStop");
            }
            catch (System.Exception e)
            {
                // It is perfectly ok we didn't have the DB or the gamestate yet.
            }

            // _engine.Suspend();
            _engine?.Exit();

            base.OnStop();
        }


        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
        }


        protected override void OnRestart()
        {
            //_engine.Resume();
            base.OnRestart();
            _triggerGame();
        }

        private EventQueue _eq = null;

        /*
         * The surface SDL renders into, and the view the IME binds to. Kept so the
         * soft-keyboard handler can reach it - see AndroidSoftKeyboard for why the
         * keyboard is bound to this view rather than raised via SDL_StartTextInput.
         */
        private GameSurface _gameSurface;

        protected override unsafe Org.Libsdl.App.SDLSurface CreateSDLSurface(Android.Content.Context? context)
        {
            var surface = new GameSurface(context);
            _gameSurface = surface;
            return surface;
        }

        /*
         * The raw-SDL2 _beforeDoEvents hook that used to live here is GONE (WP-3.5). It
         * fetched Silk's Sdl API and peeked at the event queue, but its entire body was
         * behind "#if false" - touch has always been delivered by GameSurface.OnTouch
         * instead, which is also why Sdl3WindowBackend deliberately does not translate
         * SDL's FINGER_* events: doing both would deliver every touch twice.
         */

        /// <summary>
        /// Force the game assembly to be included by the linker.
        /// This is the ONE unavoidable game-specific reference for Android.
        /// The .NET linker needs a static reference to include the assembly.
        /// </summary>
        private void _ensureGameAssemblyLoaded()
        {
            var gameAssembly = typeof(nogame.GameState).Assembly;
            engine.rom.Loader.PreRegisterAssembly(gameAssembly);
            System.Console.WriteLine("DOTNET game assembly loaded");
        }

        void _triggerGame()
        {
            lock (_lo)
            {
                if (_triggeredGame)
                {
                    return;
                }
                _triggeredGame = true;
            }

            /*
             * 1. The window. Created later, in step 9: unlike Silk's IView - which could be
             * constructed up front and initialised afterwards - SDL_CreateWindow and
             * SDL_GL_CreateContext happen together, and the GLES 3.0 attributes have to be
             * set immediately before them. Sdl3WindowBackend's constructor does all three.
             */

            // 2. Setup Android platform constants (platform-specific, not game-specific)
            GlobalSettings.Set("platform.threeD.API", "OpenGLES");
            GlobalSettings.Set("platform.threeD.API.version", "310");
            GlobalSettings.Set("engine.NailLogicalFPS", "true");
            GlobalSettings.Set("Engine.ResourcePath", "./");
            GlobalSettings.Set("Engine.GeneratedResourcePath", "./");
            GlobalSettings.Set("Android", "true");

            // 3. Register texture catalogue (engine service, not game-specific)
            I.Register<engine.joyce.TextureCatalogue>(() => new engine.joyce.TextureCatalogue());

            // 4. Setup asset implementation FIRST (platform-specific)
            var assetManagerImplementation = new Wuka.AssetImplementation(Assets);

            // 5. Load launch config via the asset system
            var launchConfig = LaunchConfig.LoadFromAssets("game.launch.json");
            
            // 6. Apply game-specific settings from config
            // Note: On Android we override some settings for mobile experience
            GlobalSettings.Set("splash.touchControls", "true");  // Always true on Android
            GlobalSettings.Set("platform.initialZoomState", launchConfig.Platform.InitialZoomState);
            GlobalSettings.Set("nogame.CreateOSD", launchConfig.Platform.CreateOSD);
            GlobalSettings.Set("nogame.CreateUI", launchConfig.Platform.CreateUI);
            GlobalSettings.Set("nogame.LogosScene.PlayTitleMusic", launchConfig.Platform.PlayTitleMusic);
            GlobalSettings.Set("Engine.RWPath", System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData));

            // 7. Force assembly loading (REQUIRED for linker)
            _ensureGameAssemblyLoaded();

            // 8. Load game config (path from launch config, not hard-coded)
            I.Register<engine.casette.Loader>(() =>
            {
                return new engine.casette.Loader(engine.Assets.Open(launchConfig.Game.ConfigPath));
            });

            assetManagerImplementation.WithLoader();
            I.Get<engine.casette.Loader>().InterpretConfig();

            // 9. Create the window and the engine over it.
            //
            // Runs on SDL's thread: SDL's Java glue spawned it, it entered libmain.so's
            // SDL_main, and that called back into SdlMain below. SDL_CreateWindow must
            // happen here rather than on the UI thread.
            _backend = new Sdl3WindowBackend("Silicon Desert 2");

            /*
             * The on-screen keyboard. Splash.Silk cannot call InputMethodManager, so the
             * backend exposes the gesture and we supply the Android half. Installed BEFORE
             * EasyCreate, so a text field that takes focus during startup still works.
             */
            _backend.SoftKeyboardHandler =
                isVisible => AndroidSoftKeyboard.SetVisible(_gameSurface, isVisible);

            _engine = Splash.Silk.Platform.EasyCreate(new string[] { }, _backend, out var silkPlatform);

            // 10. Register audio API
            I.Register<Boom.ISoundAPI>(() =>
            {
                var api = new Boom.OpenAL.API(_engine);
                return api;
            });

            // 11. Start game
            I.Get<engine.casette.Loader>().StartGame();
            
            _engine.Execute();

            I.Get<Boom.ISoundAPI>().Dispose();
        }

        /*
         * The SDL entry-point bridge, replacing Silk's SilkActivity (WP-2.2).
         *
         * SDL's Java glue calls nativeRunMain("libmain.so", "SDL_main", ...) on a thread it
         * spawns. libSDL3.so does not export SDL_main - it is a library, not an application -
         * so libmain.so provides one that invokes whatever pointer sdSetMain was given.
         * Silk shipped exactly this shim inside its .aar; WP-0.0 recovered it by disassembly
         * and recipes/build-mainshim.sh rebuilds it with a matching ABI.
         */

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void MainFunc();

        [DllImport("libmain.so", EntryPoint = "sdSetMain")]
        private static extern void SetupMain(IntPtr funcPtr);

        // Static, and kept for the process lifetime: SDL holds the raw function pointer, so
        // a collected delegate would jump into freed memory once the game starts.
        private static readonly MainFunc s_mainDelegate = SdlMain;
        private static IntPtr s_mainPtr;
        private static GameActivity s_instance;

        public override void LoadLibraries()
        {
            // Loads libSDL3.so and libmain.so, per getLibraries() in SDLActivity.java.
            base.LoadLibraries();

            s_instance = this;
            if (s_mainPtr == IntPtr.Zero)
            {
                s_mainPtr = Marshal.GetFunctionPointerForDelegate(s_mainDelegate);
                SetupMain(s_mainPtr);
            }
        }

        private static void SdlMain()
        {
            try
            {
                s_instance?._triggerGame();
            }
            catch (System.Exception e)
            {
                // Without this the exception unwinds into native SDL and the process dies
                // with no usable message.
                Android.Util.Log.Error("WUKA", "game thread failed: " + e);
            }
        }
    }
}
