using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using builtin.parts;
using engine;
using engine.joyce;
using engine.joyce.components;
using static engine.Logger;

namespace nogame.scenes.logos;

public class Scene : engine.IScene
{
    private object _lo = new();
    engine.Engine _engine;

    private DefaultEcs.World _ecsWorld;

    private engine.transform.API _aTransform;

    private DefaultEcs.Entity _eCamera;
    private DefaultEcs.Entity _eLogo;
    private DefaultEcs.Entity _eLight;

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
                _aTransform.SetPosition(_eLight, new Vector3(0f, /*-10f + 3f * t*/ 0f, 25f));
            }
            else
            {
                _t = 0f;
                /*
                 * Be totally safe and blank the camera.
                 */
                _eCamera.Get<engine.joyce.components.Camera3>().CameraMask = 0;
                _eCamera.Dispose();
                _eLight.Dispose();
                _isCleared = true;
            }
        }

        lock (_lo)
        {
            _t = t;
        }
    }


    private void _hideTitle()
    {
        _shallHideTitle = true;
    }

    public void SceneDeactivate()
    {
        engine.Engine engine = null;
        lock (_lo)
        {
            engine = _engine;
            _engine = null;
            _ecsWorld = null;
            _aTransform = null;
        }

        /*
         * Null out everything we don't need when the scene is unloaded.
         */
        engine.SceneSequencer.RemoveScene(this);
    }

    
    private void _onTitleSongStarted()
    {
        DateTime now = DateTime.Now;
        var timeline = Implementations.Get<engine.Timeline>();
        timeline.SetMarker(TimepointTitlesongStarted, DateTime.Now);
        /*
         * Fade out 4.674 after first bit of intro song. Read that one in audacity.
         */
        timeline.RunAt(
            TimepointTitlesongStarted, 
            TimeSpan.FromMilliseconds(4674),
            _hideTitle);
        
        new builtin.parts.TitlePart(new TitleCard()
        {
            StartReference = TimepointTitlesongStarted,
            StartOffset = TimeSpan.FromMilliseconds(500),
            EndReference = TimepointTitlesongStarted,
            EndOffset = TimeSpan.FromMilliseconds(2200),
            Duration = 2200-500,
            Size = new(16f, 16f),
            AlbedoTexture = new Texture("logos.joyce.albedo-joyce-engine.png"),
            EmissiveTexture = new Texture("logos.joyce.emissive-joyce-engine.png"),
            StartTransform =  new engine.transform.components.Transform3(
                true, 0x00010000, Quaternion.Identity, new Vector3(0f, 0f, 0f)),
            EndTransform =  new engine.transform.components.Transform3(
                true, 0x00010000, Quaternion.Identity, new Vector3(0f, 0.1f, -1f)),
        }).PartActivate(_engine, this);

        new builtin.parts.TitlePart(new TitleCard()
        {
            StartReference = TimepointTitlesongStarted,
            StartOffset = TimeSpan.FromMilliseconds(2000),
            EndReference = TimepointTitlesongStarted,
            EndOffset = TimeSpan.FromMilliseconds(4000),
            Duration = 2000,
            Size = new(13f, 13f/1280f*400f),
            EmissiveTexture = new Texture("titlelogo.png"),
            StartTransform =  new engine.transform.components.Transform3(
                true, 0x00010000, Quaternion.Identity, new Vector3(0f, 0f, 0f)),
            EndTransform =  new engine.transform.components.Transform3(
                true, 0x00010000, Quaternion.Identity, new Vector3(0f, 0.1f, -1f)),
        }).PartActivate(_engine, this);
    }


    private void _onTitleSongStopped()
    {
        /*
         * After title song, introduce in-game radio.
         */
    }
    
    
    public void SceneActivate(engine.Engine engine0)
    {
        lock(_lo)
        {
            _engine = engine0;

            /*
             * Some local shortcuts
             */
            _ecsWorld = _engine.GetEcsWorld();
            _aTransform = _engine.GetATransform();

        }
        if (engine.GlobalSettings.Get("nogame.LogosScene.PlayTitleMusic") != "false")
        {
            engine.Implementations.Get<Boom.Jukebox>().LoadThenPlaySong(
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
#if true
            _eLight = _engine.CreateEntity("LogosScene.PointLight");
            _eLight.Set(new engine.joyce.components.PointLight(
                new Vector4(1f, 0.95f, 0.9f, 1.0f), 15.0f));
            _aTransform.SetRotation(_eLight, 
                Quaternion.CreateFromAxisAngle(
                    new Vector3(0f, 1f, 0f), Single.Pi/2f));
            _aTransform.SetPosition(_eLight, new Vector3(0f, /*-10f + 3f * t*/ 0f, 25f));
#else
            _eLight = _engine.CreateEntity("LogosScene.DirectionalLight");
            _eLight.Set(new engine.joyce.components.DirectionalLight(
                new Vector4(1f, 1f, 1f, 1.0f)));
            _aTransform.SetRotation(_eLight, 
                Quaternion.CreateFromAxisAngle(
                    new Vector3(0f, 1f, 0f), Single.Pi/2f));
            _aTransform.SetPosition(_eLight, new Vector3(0f, /*-10f + 3f * t*/ 0f, 25f));
#endif
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
            cCamera.CameraMask = 0x00010000;
            _eCamera.Set(cCamera);
            _aTransform.SetPosition(_eCamera, new Vector3(0f, 0f, 10f));
        }


        _engine.SceneSequencer.AddScene(5, this);

    }
}
