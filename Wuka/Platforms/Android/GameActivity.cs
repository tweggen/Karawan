using System.Numerics;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl;
using Silk.NET.Input.Sdl;

using Android.Content.Res;
using Android.Views;
using engine;
using AndroidX.Core.App;
using engine.news;
using Java.Lang;
using Org.Libsdl.App;
using Silk.NET.SDL;
using Silk.NET.Windowing.Sdl.Android;
using View = Android.Views.View;

namespace Wuka
{
    [Activity(
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density,
        ScreenOrientation = ScreenOrientation.Landscape,
        Theme = "@style/Maui.SplashTheme" //"@android:style/Theme.Black.NoTitleBar.Fullscreen"
    )]
    public class GameActivity : SilkActivity, ActivityCompat.IOnRequestPermissionsResultCallback
    {
        private object _lo = new();
        private bool _triggeredGame = false;

        internal static AssetManager AssetManager;

        private Silk.NET.Windowing.IView _iView;
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

        private Silk.NET.SDL.Sdl _sdl = null;
        private EventQueue _eq = null;

        private int _eventIteration = 0;

        protected override unsafe Org.Libsdl.App.SDLSurface CreateSDLSurface(Android.Content.Context? context)
        {
            var surface = new GameSurface(context);
            return surface;
        }
        
        private void _beforeDoEvents()
        {
            
            if (null == _sdl)
            {
                System.Console.WriteLine("Fetching sdl.");
                _sdl = Silk.NET.SDL.Sdl.GetApi();
            }
            #if false

            if (null == _eq)
            {
                _eq = I.Get<EventQueue>();
            }

            int maxEvents = 100;
            ++_eventIteration;
            Silk.NET.SDL.Event[] events = new Silk.NET.SDL.Event[maxEvents];
            int nEvents = _sdl.PeepEvents(events.AsSpan(), maxEvents, Eventaction.Peekevent,
                (uint) Silk.NET.SDL.EventType.Firstevent,
                (uint) Silk.NET.SDL.EventType.Lastevent);
            for (int i = 0; i < nEvents; ++i)
            {
                Vector2 v2PhysicalPosition = new(events[i].Tfinger.X, events[i].Tfinger.Y);
                
                switch ((EventType) events[i].Type)
                {
                    case EventType.Fingerdown:
                        _eq.Push(new engine.news.Event(engine.news.Event.INPUT_FINGER_PRESSED, "")
                        {
                            PhysicalPosition = v2PhysicalPosition,
                            PhysicalSize = Vector2.One,
                            LogicalPosition = v2PhysicalPosition,
                            Data1 = (uint) events[i].Tfinger.TouchId,
                            Data2 = (uint) events[i].Tfinger.FingerId,
                            Data3 = (uint) events[i].Common.Timestamp,
                            Data4 = (uint) _eventIteration
                        });
                        break;
                    case EventType.Fingerup:
                        _eq.Push(new engine.news.Event(engine.news.Event.INPUT_FINGER_RELEASED, "")
                        {
                            PhysicalPosition = v2PhysicalPosition,
                            PhysicalSize = Vector2.One,
                            LogicalPosition = v2PhysicalPosition,
                            Data1 = (uint) events[i].Tfinger.TouchId,
                            Data2 = (uint) events[i].Tfinger.FingerId,
                            Data3 = (uint) events[i].Common.Timestamp,
                            Data4 = (uint) _eventIteration
                        });
                        break;
                    case EventType.Fingermotion:
                        _eq.Push(new engine.news.Event(engine.news.Event.INPUT_FINGER_MOVED, "")
                        {
                            PhysicalPosition = new (events[i].Tfinger.X, events[i].Tfinger.Y),
                            PhysicalSize = Vector2.One,
                            LogicalPosition = v2PhysicalPosition,
                            // TXWTODO: CHeck, if we need this size information.
                            //  = new (events[i].Tfinger.Dx, events[i].Tfinger.Dy),
                            Data1 = (uint) events[i].Tfinger.TouchId,
                            Data2 = (uint) events[i].Tfinger.FingerId,
                            Data3 = (uint) events[i].Common.Timestamp,
                            Data4 = (uint) _eventIteration
                        });
                        break;
                    default: 
                        break;
                }
            }
            #endif
        }

        /// <summary>
        /// Force the game assembly to be included by the linker.
        /// This is the ONE unavoidable game-specific reference for Android.
        /// The .NET linker needs a static reference to include the assembly.
        /// </summary>
        private void _ensureGameAssemblyLoaded()
        {
            var _ = typeof(nogame.GameState);
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

            // 1. Setup view options (platform-specific, not game-specific)
            var options = ViewOptions.Default;
            options.API = new GraphicsAPI(ContextAPI.OpenGLES, ContextProfile.Compatability, ContextFlags.Default, new APIVersion(3, 0));
            options.FramesPerSecond = 60;
            options.VSync = false;
            options.ShouldSwapAutomatically = false;
            _iView = Silk.NET.Windowing.Window.GetView(options);

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

            // 9. Create engine
            _engine = Splash.Silk.Platform.EasyCreate(new string[] { }, _iView, out var silkPlatform);
            silkPlatform.BeforeDoEvent = _beforeDoEvents;
            
            _iView.Initialize();

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

        protected override void OnRun()
        {
            /*
             * setup framework dependencies.
             */
            SdlWindowing.RegisterPlatform();
            SdlInput.RegisterPlatform();

            _triggerGame();
        }
    }
}
