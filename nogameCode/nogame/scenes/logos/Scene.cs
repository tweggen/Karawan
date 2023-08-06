using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
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
                _aTransform.SetRotation(_eLogo, Quaternion.CreateFromAxisAngle(new Vector3(0.1f, 0.9f, 0f), (t - 1f) * 2f * (float)Math.PI / 180f));
                _aTransform.SetPosition(_eLight, new Vector3(-10f + 30f * t, 0f, 25f));

                if (t >= 0.5f)
                {
                    _aTransform.SetVisible(_eLogo, true);
                    if (_engine.IsLoading())
                    {
                        /*
                         * Wait with the timeline until the scene is loaded.
                         */
                        t = 0.5f;
                    }
                    else
                    {
                        t = t;
                    }
                }

            }
            else
            {
                _t = 0f;
                _eLogo.Dispose();
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

    private DefaultEcs.Entity _createLogoBoard()
    {
        Vector2 vSize = new(16f, 16f);
        var jMesh = engine.joyce.mesh.Tools.CreatePlaneMesh("joycelogo",vSize);
        jMesh.UploadImmediately = true;
        var jMaterial = new engine.joyce.Material();
        jMaterial.UploadImmediately = true;
        jMaterial.Texture = new engine.joyce.Texture("logos.joyce.albedo-joyce-engine.png");
        jMaterial.EmissiveTexture = new engine.joyce.Texture("logos.joyce.emissive-joyce-engine.png");
        var jInstanceDesc = InstanceDesc.CreateFromMatMesh(new MatMesh(jMaterial, jMesh));

        var entity = _engine.CreateEntity("LogoBoard");
        entity.Set(new engine.joyce.components.Instance3(jInstanceDesc));
        _aTransform.SetTransforms(
            entity, false, 0x00010000,
            new Quaternion(0f, 0f, 0f, 1f),
            new Vector3(0f, 0f, 0f));
        return entity;

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
         * Joyce engine logo
         */
        {
            _eLogo = _createLogoBoard();
        }
        /*
         * Moving light
         */
        {
            _eLight = _engine.CreateEntity("LogosScene.PointLight");
            _eLight.Set(new engine.joyce.components.PointLight(
                new Vector4(1f, 0.95f, 0.9f, 1.0f), 15.0f));
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
