using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using builtin.jt;
using builtin.parts;
using engine;
using engine.joyce;
using engine.joyce.components;
using engine.news;
using engine.streets;
using engine.world;
using nogame.cities;
using nogame.config;
using nogame.modules;
using static engine.Logger;

namespace nogame.scenes.logos;

public class Scene : AModule, IScene
{
    private const uint CameraMask = 0x02000000;
    
    private engine.joyce.TransformApi _aTransform;

    private DefaultEcs.Entity _eCamera;
    private DefaultEcs.Entity _eLogo;
    private DefaultEcs.Entity _eLight;

    private bool _isCleared = false;
    private bool _shallHideTitle = false;

    /*
     * Where the login screen is. Idle accepts a login request; Connecting
     * ignores further ones and shows a running seconds counter so a stall
     * is visible; Loading means the game state is there and the loading
     * scene takes over. A failure returns to Idle so the player can retry
     * by any method.
     */
    private enum LoginState { Idle, Connecting, Loading }
    private LoginState _loginState = LoginState.Idle;
    private string _loginActivity = "";
    private DateTime _loginStartedAt;
    private int _loginSecondsShown = -1;
    private bool _isGameSetupActive = false;

    /*
     * After this long, the status line admits that this is taking long.
     */
    private const int LoginSlowSeconds = 20;
  
    private bool _isAnimRunning = false;
    private float _t;
    
    public static string TimepointTitlesongStarted = "nogame.scenes.logos.titlesong.Started"; 

    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        /*
         * We will activate gameplay from startup.
         */
        new SharedModule<nogame.modules.Gameplay>() { ShallActivate = false },
        new SharedModule<nogame.modules.GameSetup>() { ShallActivate = false },
        new SharedModule<nogame.config.Module>() ,
        new SharedModule<nogame.modules.AutoSave>(),
        new SharedModule<builtin.jt.Factory>(),
        new SharedModule<nogame.modules.story.Narration>(),
        new MyModule<nogame.modules.menu.LoginMenuModule> { ShallActivate = false },
        new MyModule<TitleController> { ShallActivate = false }
    };
  
  
    private void _animateTitle(float dt)
    {
        /*
         * Implement some animation into the title.
         */
        float t;
        lock (_lo)
        {
            t = _t + dt;
        }
        
        if (_isCleared)
        {
        }
        else
        {
            if (!_shallHideTitle)
            {
                float z = float.Floor(3f*t)/3f + t/8f;
                _aTransform.SetPosition(_eCamera, new Vector3(0f, 0f, 27f + z/10f));
                _aTransform.SetPosition(_eLight, new Vector3(0f, -10f + 3f * t, 25f));
            }
            else
            {
                _t = 0f;
                /*
                 * Be totally safe and blank the camera.
                 */
                _eCamera.Get<engine.joyce.components.Camera3>().CameraMask = 0;
                _isCleared = true;
            }
        }

        lock (_lo)
        {
            _t = t;
        }
    }
    
    
    public void SceneOnLogicalFrame(float dt)
    {
        _animateTitle(dt);
        _tickLoginStatus();
    }

    private bool _hideTitle()
    {
        _shallHideTitle = true;
        return true;
    }


    private bool _loadLoadingScene()
    {
        I.Get<SceneSequencer>().SetMainScene("loading");
        return true;
    }
    

    private bool _loadRootScene()
    {
        I.Get<SceneSequencer>().SetMainScene("root");
        return true;
    }


    private bool _preload()
    {
        /*
         * Use a local copy of engine as this task may outlive the lifetime of
         * this module.
         */
        var engine = _engine;
        
        /*
         * Start preloading in the background.
         */
        _engine.QueueMainThreadAction(() =>
        {
            I.Get<SetupMetaGen>().PrepareMetaGen(engine);
        });

        return true;
    }


    /**
     * Must be called on the logical thread.
     */
    private void _setLoginWidget(string id, string attr, object value)
    {
        try
        {
            if (M<Factory>().Layer("pausemenu").GetChild(id, out var widget))
            {
                widget[attr] = value;
            }
        }
        catch (Exception)
        {
            // Login menu UI may not be available (e.g., auto-login mode)
        }
    }


    private void _displayLoginStatus(string status, string hint = "")
    {
        _engine.RunMainThread(() =>
        {
            _setLoginWidget("menuLoginStatusText", "text", status);
            _setLoginWidget("menuLoginHintText", "text", hint);
        });
    }


    private void _displayStartButtonBusy(bool isBusy)
    {
        _engine.RunMainThread(() =>
        {
            _setLoginWidget("ui.loginStart", "text", isBusy ? "WAIT" : "START");
            _setLoginWidget("ui.loginStart", "fillColor", isBusy ? "#444444" : "#448822");
        });
    }


    /**
     * Claim the login screen for one attempt. Returns false if an attempt
     * is already under way; the visible counter already tells the player so.
     */
    private bool _tryBeginLogin(string activity)
    {
        lock (_lo)
        {
            if (_loginState != LoginState.Idle) return false;
            _loginState = LoginState.Connecting;
            _loginActivity = activity;
            _loginStartedAt = DateTime.Now;
            _loginSecondsShown = -1;
        }

        _displayLoginStatus($"{activity}...");
        _displayStartButtonBusy(true);
        return true;
    }


    private void _tickLoginStatus()
    {
        string activity;
        int seconds;
        lock (_lo)
        {
            if (_loginState != LoginState.Connecting) return;
            seconds = (int)(DateTime.Now - _loginStartedAt).TotalSeconds;
            if (seconds == _loginSecondsShown) return;
            _loginSecondsShown = seconds;
            activity = _loginActivity;
        }

        _displayLoginStatus(
            $"{activity}... {seconds}s",
            seconds >= LoginSlowSeconds ? "this is taking long, the server may be slow or unreachable." : "");
    }
    

    private void _onAutoSaveSetup(GameState gs)
    {
        /*
         * From here on the game is being set up, so the login screen takes
         * no more requests, not even after a failure: a half-started game
         * cannot be started a second time.
         */
        lock (_lo)
        {
            _loginState = LoginState.Loading;
        }

        try
        {
            /*
             * Remember how we logged in for next time.
             */
            if (M<AutoSave>().SyncOnline)
            {
                M<nogame.config.Module>().GameConfig.LoginMode = (int) GameConfig.Mode.LoginGlobally;
            }
            else
            {
                M<nogame.config.Module>().GameConfig.LoginMode = (int) GameConfig.Mode.LoginLocally;
            }
            M<nogame.config.Module>().Save();

            /*
             * After we have a game state, start the gameplay.
             */
            M<Gameplay>().ModuleActivate();
        }
        catch (Exception e)
        {
            Error($"Unable to start the game: {e}");
            _displayLoginStatus($"unable to start the game: {ServerErrorText.Describe(e)}", "please restart the game.");
            return;
        }

        _displayLoginStatus(M<AutoSave>().SyncOnline ? "logged in." : "game loaded.");

        _engine.QueueMainThreadAction(() =>
        {
            try { DeactivateMyModule<nogame.modules.menu.LoginMenuModule>(); }
            catch (Exception) { /* LoginMenuModule may not be active in auto-login mode */ }
        });
        _engine.QueueMainThreadAction(() => _loadLoadingScene());
        _engine.QueueMainThreadAction(() => {

            /*
            * Preload the player position from the current gamestate.
            */
            I.Get<SetupMetaGen>().Preload(gs.PlayerPosition);

            /*
             * Show the root scene earliest at this point.
             */
            I.Get<engine.Timeline>().RunAt(
                TimepointTitlesongStarted,
                TimeSpan.FromMilliseconds(4674),
                _loadRootScene);
        });
    }


    /**
     * Called on any thread. Autosave is inactive again at this point, so
     * the player may retry with the same or another method.
     */
    private void _onAutoSaveError(string errorText)
    {
        bool wasOnline = M<AutoSave>().SyncOnline;
        lock (_lo)
        {
            _loginState = LoginState.Idle;
        }

        _displayLoginStatus(
            errorText,
            wasOnline
                ? "check eMail and password and retry, or choose local machine to play offline."
                : "select a login method to try again.");
        _displayStartButtonBusy(false);
    }
    

    private void _startGame(bool reset)
    {
        /*
         * Before loading the game with the parameters given,
         * setupup game state setup tools.
         *
         * TXWTODO: Shouldn't they be required on reset only?
         *
         * A failed attempt leaves it active; a retry must not activate it twice.
         */
        if (!_isGameSetupActive)
        {
            _isGameSetupActive = true;
            M<GameSetup>().ModuleActivate();
        }
        
        /*
         * Give us a short delay to render some frames.
         */
        I.Get<engine.Timeline>().RunIn(TimeSpan.FromMilliseconds(200),
            () =>
            {
                M<AutoSave>().StartAutoSave(reset, _onAutoSaveSetup, _onAutoSaveError);
            });
    }


    private void _onLoginLocally(Event ev)
    {
        if (!_tryBeginLogin("loading the local game")) return;

        I.Get<AutoSave>().SyncOnline = false;

        _startGame(false);
    }


    private void _onLoginGlobally(Event ev)
    {
        if (!_tryBeginLogin("logging in to the global machine")) return;

        I.Get<AutoSave>().SyncOnline = true;

        _startGame(false);
    }


    /**
     * The big START button is for touch screens: it starts whichever login
     * method the menu has selected, which is the last-used one unless the
     * player moved the selection.
     */
    private void _onStartButton(Event ev)
    {
        bool loginLocally = M<nogame.config.Module>().GameConfig.LoginMode == (int)GameConfig.Mode.LoginLocally;
        try
        {
            var wRoot = M<Factory>().Layer("pausemenu");
            var wFocussed = wRoot.GetFocussedChild();
            if (null != wFocussed)
            {
                if (wRoot.GetChild("ui.loginGlobally", out var wGlobally) && wFocussed == wGlobally)
                {
                    loginLocally = false;
                }
                else if (wRoot.GetChild("ui.loginLocally", out var wLocally) && wFocussed == wLocally)
                {
                    loginLocally = true;
                }
            }
        }
        catch (Exception)
        {
            // No menu, keep the remembered mode.
        }

        if (loginLocally)
        {
            _onLoginLocally(ev);
        }
        else
        {
            _onLoginGlobally(ev);
        }
    }


    private void _onNewGlobally(Event ev)
    {
        if (!_tryBeginLogin("logging in to start a new global game")) return;

        I.Get<AutoSave>().SyncOnline = true;

        _startGame(true);
    }


    public void SceneKickoff()
    {
    }


    private void _showLoginMenu()
    {
        Subscribe("nogame.login.loginLocally", _onLoginLocally);
        Subscribe("nogame.login.loginGlobally", _onLoginGlobally);
        Subscribe("nogame.login.newGlobally", _onNewGlobally);
        Subscribe("nogame.login.Start", _onStartButton);

        ActivateMyModule<nogame.modules.menu.LoginMenuModule>();
        
        /*
         * Handle autologin if requested
         */
        switch (engine.GlobalSettings.Get("debug.option.autoLogin"))
        {
            case "global":
            case "globally":
                I.Get<EventQueue>().Push(new Event("nogame.login.loginGlobally", ""));
                break;
            case "local":
            case "locally":
                I.Get<EventQueue>().Push(new Event("nogame.login.loginLocally", ""));
                break;
            case "new":
                I.Get<EventQueue>().Push(new Event("nogame.login.newGlobally", ""));
                break;
            default:
                break;
        }
    }

    
    protected override void OnModuleDeactivate()
    {
        _engine.AddDoomedEntity(_eCamera);
        _engine.AddDoomedEntity(_eLight);
        lock (_lo)
        {
            _aTransform = null;
        }

        /*
         * Null out everything we don't need when the scene is unloaded.
         */
        I.Get<SceneSequencer>().RemoveScene(this);

        UnsubscribeAll();
    }

    
    private void _onTitleSongStarted()
    {
        _engine.QueueMainThreadAction(() =>
        {
            _isAnimRunning = true;

            DateTime now = DateTime.Now;
            var timeline = I.Get<engine.Timeline>();
            timeline.SetMarker(TimepointTitlesongStarted, DateTime.Now);

            _showLoginMenu();

            /*
             * Start preloading after the first title starts display
             */
            timeline.RunAt(
                TimepointTitlesongStarted,
                TimeSpan.FromMilliseconds(800),
                _preload);

            /*
             * Blank 4.674 after first bit of intro song. Read that one in audacity.
             */
            timeline.RunAt(
                TimepointTitlesongStarted,
                TimeSpan.FromMilliseconds(4474),
                _hideTitle);

            {
                var modTitle = M<TitleController>();

                /*
                 * We do not use the texture atlas because there is no need to waste previous atlas space
                 * with these intro logos.
                 */
                modTitle.Add(new TitleCard()
                {
                    StartReference = TimepointTitlesongStarted,
                    StartOffset = TimeSpan.FromMilliseconds(500),
                    EndReference = TimepointTitlesongStarted,
                    EndOffset = TimeSpan.FromMilliseconds(3000),
                    Duration = 2500,
                    Flags = (uint)TitleCard.F.FadeoutEnd,
                    FadeOutTime = 500f,
                    Size = new(14f, 7f),
                    EmissiveTexture =
                        new Texture(
                            "aihao-emissive.png"), // I.Get<TextureCatalogue>().FindTexture("aihao-emissive.png"),
                    StartTransform = new engine.joyce.components.Transform3(
                        true, CameraMask, Quaternion.Identity, new Vector3(0f, 0f, 0f), Vector3.One * 1.3f),
                    EndTransform = new engine.joyce.components.Transform3(
                        true, CameraMask, Quaternion.Identity, new Vector3(0f, 0f, 0f), Vector3.One * 1.3f)
                });
                modTitle.Add(new TitleCard()
                {
                    StartReference = TimepointTitlesongStarted,
                    StartOffset = TimeSpan.FromMilliseconds(2400),
                    EndReference = TimepointTitlesongStarted,
                    EndOffset = TimeSpan.FromMilliseconds(3900),
                    Duration = 1500,
                    Flags = (uint)TitleCard.F.JitterEnd,
                    Size = new(64f, 64f / 1280f * 220f),
                    AlbedoTexture =
                        new Texture(
                            "silicondesert-albedo.png"), // I.Get<TextureCatalogue>().FindTexture("silicondesert-albedo.png"),
                    EmissiveTexture =
                        new Texture(
                            "silicondesert-emissive.png"), // I.Get<TextureCatalogue>().FindTexture("silicondesert-emissive.png"),
                    StartTransform = new engine.joyce.components.Transform3(
                        true, CameraMask, Quaternion.Identity, new Vector3(0f, -4.9f, -7f), Vector3.One * 0.64f),
                    EndTransform = new engine.joyce.components.Transform3(
                        true, CameraMask, Quaternion.Identity, new Vector3(0f, -4.9f, -7f), Vector3.One * 0.64f)
                });
            }

            ActivateMyModule<TitleController>();
        });
    }


    private void _onTitleSongStopped()
    {
        /*
         * After title song, introduce in-game radio.
         */
    }
    
    
    protected override void OnModuleActivate()
    {
        /*
         * Some local shortcuts
         */
        _aTransform = I.Get<engine.joyce.TransformApi>();

        /*
         * Moving light
         */
        {
            _eLight = _engine.CreateEntity("LogosScene.PointLight");
            _eLight.Set(new engine.joyce.components.PointLight(
                new Vector4(1f, 0.95f, 0.9f, 1.0f), 15.0f));
            _aTransform.SetRotation(_eLight,
                Quaternion.CreateFromAxisAngle(
                    new Vector3(0f, 1f, 0f), Single.Pi/2f));
            _aTransform.SetPosition(_eLight, new Vector3(0f, /*-10f + 3f * t*/ 0f, 25f));
        }

        /*
         * Create a camera.
         */
        {
            _eCamera = _engine.CreateEntity("LogosScene.Camera");
            var cCamera = new engine.joyce.components.Camera3();
            cCamera.Angle = 60.0f;
            cCamera.NearFrustum = 1f;

            /*
             * We need to be as far away as the skycube is. Plus a bonus.
             */
            cCamera.FarFrustum = (float)100f;
            cCamera.CameraMask = CameraMask;
            _eCamera.Set(cCamera);
            _aTransform.SetVisible(_eCamera, true);
            _aTransform.SetCameraMask(_eCamera, CameraMask);
            _aTransform.SetPosition(_eCamera, new Vector3(0f, 0f, 10f));
        }

        I.Get<Boom.ISoundAPI>().SoundMask = 0xffff0000;
        I.Get<SceneSequencer>().AddScene(5, this);

        string autoLogin = engine.GlobalSettings.Get("debug.option.autoLogin");
        bool hasAutoLogin = !string.IsNullOrEmpty(autoLogin) && autoLogin != "false";

        if (hasAutoLogin)
        {
            /*
             * Skip title song for automated/test startup — defer until the
             * main game loop is running so Timeline scheduling works.
             */
            _engine.QueueMainThreadAction(() =>
            {
                var timeline = I.Get<engine.Timeline>();
                timeline.SetMarker(TimepointTitlesongStarted, DateTime.Now);
                _tryBeginLogin("starting");

                // Preload MetaGen (normally scheduled from _onTitleSongStarted)
                _preload();

                bool reset = (autoLogin == "new");
                bool syncOnline = (autoLogin == "global" || autoLogin == "globally");
                I.Get<AutoSave>().SyncOnline = syncOnline;
                _startGame(reset);
            });
        }
        else
        {
            bool shouldPlayTitle = engine.GlobalSettings.Get("nogame.LogosScene.PlayTitleMusic") != "false";
            engine.I.Get<Boom.Jukebox>().LoadThenPlaySong(
                "shaklengokhsi.ogg", shouldPlayTitle?0.05f:0f, false,
                _onTitleSongStarted, _onTitleSongStopped);
        }
    }
}
