using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using builtin.parts;
using engine;
using engine.joyce;
using engine.joyce.components;
using engine.news;
using engine.streets;
using engine.world;
using nogame.cities;
using nogame.modules;
using static engine.Logger;

namespace nogame.scenes.logos;

public class Scene : AModule, IScene 
{
    private engine.joyce.TransformApi _aTransform;

    private DefaultEcs.Entity _eCamera;
    private DefaultEcs.Entity _eLogo;
    private DefaultEcs.Entity _eLight;

    private bool _isCleared = false;
    private bool _shallHideTitle = false;
    private bool _isStartingGame = false;
  
    private bool _isAnimRunning = false;
    private float _t;
    
    public static string TimepointTitlesongStarted = "nogame.scenes.logos.titlesong.Started"; 

    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<nogame.modules.AutoSave>(),
        new MyModule<nogame.modules.menu.LoginMenuModule> { ShallActivate = false },
        new MyModule<TitleModule> { ShallActivate = false }
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
            I.Get<MetaGen>().ClusterOperators.Add(new GenerateShopsOperator());
            I.Get<MetaGen>().ClusterOperators.Add(new GenerateClusterNavLanesOperator());
            I.Get<SetupMetaGen>().PrepareMetaGen(engine);
        });

        return true;
    }


    private void _onAutoSaveSetup(GameState gs)
    {
        _engine.QueueMainThreadAction(() =>
        {
            _loadLoadingScene();
        
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
    

    private void _startGame()
    {
        /*
         * Give us a short delay to render some frames.
         */
        I.Get<engine.Timeline>().RunIn(TimeSpan.FromMilliseconds(200),
            () => M<AutoSave>().StartAutoSave(_onAutoSaveSetup));
    }


    private void _onLoginLocally(Event ev)
    {
        lock (_lo)
        {
            if (_isStartingGame) return;
            _isStartingGame = true;
        }
        
        DeactivateMyModule<nogame.modules.menu.LoginMenuModule>();
        I.Get<AutoSave>().SyncOnline = false; 

        _startGame();
    }
    

    private void _onLoginGlobally(Event ev)
    {
        lock (_lo)
        {
            if (_isStartingGame) return;
            _isStartingGame = true;
        }

        DeactivateMyModule<nogame.modules.menu.LoginMenuModule>();
        I.Get<AutoSave>().SyncOnline = true;

        _startGame();
    }
    

    public void SceneKickoff()
    {
    }


    private void _showLoginMenu()
    {
        I.Get<SubscriptionManager>().Subscribe("nogame.login.loginLocally", _onLoginLocally);
        I.Get<SubscriptionManager>().Subscribe("nogame.login.loginGlobally", _onLoginGlobally);
        ActivateMyModule<nogame.modules.menu.LoginMenuModule>();
    }

    
    public override void ModuleDeactivate()
    {
        _engine.RemoveModule(this);

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
        
        I.Get<SubscriptionManager>().Unsubscribe("nogame.login.loginLocally", _onLoginLocally);
        I.Get<SubscriptionManager>().Unsubscribe("nogame.login.loginGlobally", _onLoginGlobally);

        base.ModuleDeactivate();
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
                var modTitle = M<TitleModule>();

                /*
                 * We do not use the texture atlas because there is no need to waste previous atlas space
                 * with these intro logos.
                 */
                modTitle.Add(new TitleCard()
                {
                    StartReference = TimepointTitlesongStarted,
                    StartOffset = TimeSpan.FromMilliseconds(500),
                    EndReference = TimepointTitlesongStarted,
                    EndOffset = TimeSpan.FromMilliseconds(1200),
                    Duration = 700,
                    Flags = (uint)TitleCard.F.FadeoutEnd,
                    FadeOutTime = 500f,
                    Size = new(14f, 7f),
                    EmissiveTexture =
                        new Texture(
                            "aihao-emissive.png"), // I.Get<TextureCatalogue>().FindTexture("aihao-emissive.png"),
                    StartTransform = new engine.joyce.components.Transform3(
                        true, 0x01000000, Quaternion.Identity, new Vector3(0f, 0f, 0f), Vector3.One * 1.3f),
                    EndTransform = new engine.joyce.components.Transform3(
                        true, 0x01000000, Quaternion.Identity, new Vector3(0f, 0f, 0f), Vector3.One * 1.3f)
                });
                modTitle.Add(new TitleCard()
                {
                    StartReference = TimepointTitlesongStarted,
                    StartOffset = TimeSpan.FromMilliseconds(1400),
                    EndReference = TimepointTitlesongStarted,
                    EndOffset = TimeSpan.FromMilliseconds(2900),
                    Duration = 700,
                    Flags = (uint)TitleCard.F.JitterEnd,
                    Size = new(64f, 64f / 1280f * 220f),
                    AlbedoTexture =
                        new Texture(
                            "silicondesert-albedo.png"), // I.Get<TextureCatalogue>().FindTexture("silicondesert-albedo.png"),
                    EmissiveTexture =
                        new Texture(
                            "silicondesert-emissive.png"), // I.Get<TextureCatalogue>().FindTexture("silicondesert-emissive.png"),
                    StartTransform = new engine.joyce.components.Transform3(
                        true, 0x01000000, Quaternion.Identity, new Vector3(0f, -4.9f, -7f), Vector3.One * 0.64f),
                    EndTransform = new engine.joyce.components.Transform3(
                        true, 0x01000000, Quaternion.Identity, new Vector3(0f, -4.9f, -7f), Vector3.One * 0.64f)
                });
            }

            ActivateMyModule<TitleModule>();
        });
    }


    private void _onTitleSongStopped()
    {
        /*
         * After title song, introduce in-game radio.
         */
    }
    
    
    public override void ModuleActivate()
    {
        base.ModuleActivate();

    /*
         * Some local shortcuts
         */
        _aTransform = I.Get<engine.joyce.TransformApi>();

        bool shouldPlayTitle = engine.GlobalSettings.Get("nogame.LogosScene.PlayTitleMusic") != "false";
        
        engine.I.Get<Boom.Jukebox>().LoadThenPlaySong(
            "shaklengokhsi.ogg", shouldPlayTitle?0.05f:0f, false,
            _onTitleSongStarted, _onTitleSongStopped);

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
            cCamera.CameraMask = 0x01000000;
            _eCamera.Set(cCamera);
            _aTransform.SetVisible(_eCamera, true);
            _aTransform.SetCameraMask(_eCamera, 0x01000000);
            _aTransform.SetPosition(_eCamera, new Vector3(0f, 0f, 10f));
        }


        _engine.AddModule(this);

        I.Get<Boom.ISoundAPI>().SoundMask = 0xffff0000;
        I.Get<SceneSequencer>().AddScene(5, this);
    }
}
