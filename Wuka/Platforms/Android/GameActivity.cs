using Android.App;
using Android.App.Roles;
using Android.Content.PM;
using Android.OS;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl;
using Silk.NET.Input.Sdl;

using Android.Content.Res;
using engine;
using Silk.NET.GLFW;
using Android.Media;
using Wuka.Platforms.Android;
using Java.Util;
using System.Numerics;
using Android;
using Android.Runtime;
using Android.Util;
using Android.Widget;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using engine.news;
using Xamarin.Essentials;
using nogame;
using Silk.NET.SDL;
using GameState = Android.App.GameState;

namespace Wuka
{
    [Activity(
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density,
        ScreenOrientation = ScreenOrientation.Landscape,
        Theme = "@style/Maui.SplashTheme" //"@android:style/Theme.Black.NoTitleBar.Fullscreen"
    )]
    public class GameActivity : Silk.NET.Windowing.Sdl.Android.SilkActivity, ActivityCompat.IOnRequestPermissionsResultCallback
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
                I.Get<engine.DBStorage>()?.SaveGameState(I.Get<GameState>());
            }
            catch (Exception e)
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

        private void _beforeDoEvents()
        {
            if (null == _sdl)
            {
                System.Console.WriteLine("Fetching sdl.");
                _sdl = Silk.NET.SDL.Sdl.GetApi();
            }

            if (null == _eq)
            {
                _eq = I.Get<EventQueue>();
            }

            int maxEvents = 100;
            Silk.NET.SDL.Event[] events = new Silk.NET.SDL.Event[maxEvents];
            int nEvents = _sdl.PeepEvents(events.AsSpan(), maxEvents, Eventaction.Peekevent,
                (uint) Silk.NET.SDL.EventType.Firstevent,
                (uint) Silk.NET.SDL.EventType.Lastevent);
            for (int i = 0; i < nEvents; ++i)
            {
                switch ((EventType) events[i].Type)
                {
                    case EventType.Fingerdown:
                        _eq.Push(new engine.news.Event(engine.news.Event.INPUT_FINGER_PRESSED, "")
                        {
                            Position = new (events[i].Tfinger.X, events[i].Tfinger.Y),
                            Data1 = (uint) events[i].Tfinger.TouchId,
                            Data2 = (uint) events[i].Tfinger.FingerId
                        });
                        break;
                    case EventType.Fingerup:
                        _eq.Push(new engine.news.Event(engine.news.Event.INPUT_FINGER_RELEASED, "")
                        {
                            Position = new (events[i].Tfinger.X, events[i].Tfinger.Y),
                            Data1 = (uint) events[i].Tfinger.TouchId,
                            Data2 = (uint) events[i].Tfinger.FingerId
                        });
                        break;
                    case EventType.Fingermotion:
                        _eq.Push(new engine.news.Event(engine.news.Event.INPUT_FINGER_MOVED, "")
                        {
                            Position = new (events[i].Tfinger.X, events[i].Tfinger.Y),
                            Size = new (events[i].Tfinger.Dx, events[i].Tfinger.Dy),
                            Data1 = (uint) events[i].Tfinger.TouchId,
                            Data2 = (uint) events[i].Tfinger.FingerId
                        });
                        break;
                    default: 
                        break;
                }
            }
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
             * Setup singletons and statics
             */
            var assetManagerImplementation = new Wuka.AssetImplementation(Assets);
            engine.Assets.SetAssetImplementation(assetManagerImplementation);

            var options = ViewOptions.Default;
            options.API = new GraphicsAPI(ContextAPI.OpenGLES, ContextProfile.Compatability, ContextFlags.Default, new APIVersion(3, 0));
            options.FramesPerSecond = 60;
            options.VSync = false;
            options.ShouldSwapAutomatically = false;
            _iView = Silk.NET.Windowing.Window.GetView(options); // note also GetView, instead of Window.Create.

            engine.GlobalSettings.Set("nogame.CreateOSD", "true");
            engine.GlobalSettings.Set("platform.threeD.API", "OpenGLES");
            engine.GlobalSettings.Set("platform.threeD.API.version", "300");
            engine.GlobalSettings.Set("engine.NailLogicalFPS", "true");
            engine.GlobalSettings.Set("Engine.ResourcePath", "./");
            engine.GlobalSettings.Set("splash.touchControls", "true");
            engine.GlobalSettings.Set("Android", "true");
            engine.GlobalSettings.Set("platform.initialZoomState", "-16");
            engine.GlobalSettings.Set("nogame.CreateUI", "false");
            engine.GlobalSettings.Set("nogame.LogosScene.PlayTitleMusic", "true");
            engine.GlobalSettings.Set("Engine.RWPath", System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData));

            _engine = Splash.Silk.Platform.EasyCreate(new string[] { }, _iView, out var silkPlatform);
            silkPlatform.BeforeDoEvent = _beforeDoEvents;
            
            _iView.Initialize();

            I.Register<engine.joyce.TextureCatalogue>(() => new engine.joyce.TextureCatalogue());
            I.Register<Boom.ISoundAPI>(() =>
            {
                var api = new Boom.OpenAL.API(_engine);
                return api;
            });

            /*
             * We need to explicitly reference the game.
             */
            {
                var rootDepends = new nogame.GameState();
                System.Console.WriteLine("DOTNET silicon desert "+rootDepends);
            }
            engine.casette.Loader.LoadStartGame("nogame.json");
            
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