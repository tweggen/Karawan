using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using builtin.parts;
using engine;
using engine.joyce;
using engine.joyce.components;
using engine.world;
using static engine.Logger;

namespace nogame.scenes.logos;

public class Scene : AModule, IScene 
{
    private engine.joyce.TransformApi _aTransform;

    private DefaultEcs.Entity _eCamera;
    private DefaultEcs.Entity _eLogo;
    private DefaultEcs.Entity _eLight;

    private TitleModule _modTitle;
    
    private bool _isCleared = false;

    private bool _shallHideTitle = false;

    private float _t;
    
    public static string TimepointTitlesongStarted = "nogame.scenes.logos.titlesong.Started"; 

    public void SceneOnLogicalFrame(float dt)
    {
        float t;
        float tBefore;
        lock (_lo)
        {
            tBefore = _t;
            t = _t + dt;
        }
        
        if (_isCleared)
        {
            /*
             * Immediately trigger main scene. It will setup loading.
             */
            _engine.SceneSequencer.SetMainScene("root");
        }
        else
        {
            if (!_shallHideTitle)
            {
                _aTransform.SetPosition(_eCamera, new Vector3(0f, 0f, 20f + _t));
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


    private bool _hideTitle()
    {
        _shallHideTitle = true;
        return true;
    }


    private bool _preload()
    {
        /*
         * Start preloading in the background.
         */
        Task.Run(() =>
        {
            I.Get<SetupMetaGen>().PrepareMetaGen(_engine);
            
            /*
             * Preload the player position from the current gamestate.
             */
            I.Get<SetupMetaGen>().Preload(I.Get<GameState>().PlayerPosition);
        });

        return true;
    }
    

    public void SceneKickoff()
    {
    }
    
    
    public void ModuleDeactivate()
    {
        _engine.RemoveModule(this);
        _modTitle.ModuleDeactivate();

        /*
         * 
         */
        _engine.AddDoomedEntity(_eCamera);
        _engine.AddDoomedEntity(_eLight);
        engine.Engine engine = null;
        lock (_lo)
        {
            engine = _engine;
            _aTransform = null;
        }

        /*
         * Null out everything we don't need when the scene is unloaded.
         */
        engine.SceneSequencer.RemoveScene(this);
        base.ModuleDeactivate();
    }

    
    private void _onTitleSongStarted()
    {
        DateTime now = DateTime.Now;
        var timeline = I.Get<engine.Timeline>();
        timeline.SetMarker(TimepointTitlesongStarted, DateTime.Now);
        
        /*
         * Fade out 4.674 after first bit of intro song. Read that one in audacity.
         */
        timeline.RunAt(
            TimepointTitlesongStarted, 
            TimeSpan.FromMilliseconds(4674),
            _hideTitle);

        /*
         * Start preloading after the first title starts display
         */
        timeline.RunAt(
            TimepointTitlesongStarted, 
            TimeSpan.FromMilliseconds(800),
            _preload);

        _modTitle.Add(new TitleCard()
        {
            StartReference = TimepointTitlesongStarted,
            StartOffset = TimeSpan.FromMilliseconds(500),
            EndReference = TimepointTitlesongStarted,
            EndOffset = TimeSpan.FromMilliseconds(2200),
            Duration = 2200-500,
            Size = new(16f, 16f),
            AlbedoTexture = new Texture("logos.joyce.albedo-joyce-engine.png"),
            EmissiveTexture = new Texture("logos.joyce.emissive-joyce-engine.png"),
            StartTransform =  new engine.joyce.components.Transform3(
                true, 0x01000000, Quaternion.Identity, Vector3.Zero),
            EndTransform =  new engine.joyce.components.Transform3(
                true, 0x01000000, Quaternion.Identity, Vector3.Zero),
        });

        _modTitle.Add(new TitleCard()
        {
            StartReference = TimepointTitlesongStarted,
            StartOffset = TimeSpan.FromMilliseconds(2000),
            EndReference = TimepointTitlesongStarted,
            EndOffset = TimeSpan.FromMilliseconds(4000),
            Duration = 2000,
            Size = new(40f, 40f/1280f*400f),
            EmissiveTexture = new Texture("titlelogo.png"),
            StartTransform =  new engine.joyce.components.Transform3(
                true, 0x01000000, Quaternion.Identity, new Vector3(0f, 0f, 0f)),
            EndTransform =  new engine.joyce.components.Transform3(
                true, 0x01000000, Quaternion.Identity, new Vector3(0f, 0.1f, -1f)),
        });
        
        _modTitle.ModuleActivate(_engine);
    }


    private void _onTitleSongStopped()
    {
        /*
         * After title song, introduce in-game radio.
         */
    }
    
    
    public override void ModuleActivate(engine.Engine engine0)
    {
        base.ModuleActivate(engine0);
        lock(_lo)
        {
            /*
             * Some local shortcuts
             */
            _aTransform = I.Get<engine.joyce.TransformApi>();

        }
        if (engine.GlobalSettings.Get("nogame.LogosScene.PlayTitleMusic") != "false")
        {
            engine.I.Get<Boom.Jukebox>().LoadThenPlaySong(
                "shaklengokhsi.ogg", 0.05f, false,
                _onTitleSongStarted, _onTitleSongStopped);
        }
        else
        {
            _onTitleSongStarted();
            _onTitleSongStopped();
        }

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
        _engine.SceneSequencer.AddScene(5, this);
        _modTitle = new();
        
    }
}
