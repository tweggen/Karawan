using System;
using System.Numerics;
using engine;
using engine.joyce;
using engine.joyce.components;
using engine.news;
using engine.world;
using static engine.Logger;

namespace nogame.modules;

public class World : AModule
{
    private engine.world.Loader _worldLoader;
    private engine.world.MetaGen _worldMetaGen;
    
    private engine.joyce.TransformApi _aTransform;
    
    private DefaultEcs.Entity _eCamScene;
    private DefaultEcs.Entity ELightMain;
    private DefaultEcs.Entity ELightBack;
    public DefaultEcs.Entity EAmbientLight;


    private void _triggerLoadWorld()
    {
        Vector3 vMe;
        if (!_eCamScene.Has<Transform3ToWorld>())
        {
            return;
        }
        
        vMe = _eCamScene.Get<Transform3ToWorld>().Matrix.Translation;
        // TXWTODO: We don't precisely know when we have the first valid position 
        if (vMe != Vector3.Zero)
        {
            if (_worldLoader == null)
            {
                ErrorThrow("WorldLoader is null here?", m => new InvalidOperationException(m));
            }
            _worldLoader.WorldLoaderProvideFragments();
        }
    }
    
    
    private void _onRootKickoff(Event ev)
    {
        _eCamScene.Get<engine.joyce.components.Camera3>().CameraFlags &=
            ~engine.joyce.components.Camera3.Flags.PreloadOnly;
    }


    private void _onLogicalFrame(object? sender, float dt)
    {
        _triggerLoadWorld();
    }
    
    
    public override void ModuleDeactivate()
    {
        _engine.OnLogicalFrame -= _onLogicalFrame;
        _engine.RemoveModule(this);

        I.Get<SubscriptionManager>().Unsubscribe("nogame.scenes.root.Scene.kickoff", _onRootKickoff);
        _aTransform = null;
        base.ModuleDeactivate();
    }
    
    
    public override void ModuleActivate()
    {
        base.ModuleActivate();
        
        _worldMetaGen = I.Get<MetaGen>();
        _worldLoader = _worldMetaGen.Loader;
        if (null == _worldLoader)
        {
            ErrorThrow("_worldLoader is not supposed to be null here.", m => new InvalidOperationException(m));
            return;
        }

       
        /*
         * trigger generating the world at the starting point.
         */ 
        _triggerLoadWorld();
        
        _aTransform = I.Get<TransformApi>();
        
        /*
         * Global objects.
         */
        
        /*
         * Directional light
         */
        {
            ELightMain = _engine.CreateEntity("RootScene.DirectionalLight");
            ELightMain.Set(new engine.joyce.components.DirectionalLight(new Vector4(0.7f, 0.8f, 0.9f, 0.0f)));
            _aTransform.SetRotation(ELightMain, Quaternion.CreateFromAxisAngle(new Vector3(0, 0, -1), 45f * (float)Math.PI / 180f));
        }
        {
            ELightBack = _engine.CreateEntity("RootScene.OtherLight");
            ELightBack.Set(new engine.joyce.components.DirectionalLight(new Vector4(0.2f, 0.2f, 0.0f, 0.0f)));
            _aTransform.SetRotation(ELightBack, Quaternion.CreateFromAxisAngle(new Vector3(0, 1, 0), 180f * (float)Math.PI / 180f));
        }
        
        /*
         * Ambient light
         */
        {
            EAmbientLight = _engine.CreateEntity("RootScene.AmbientLight");
            EAmbientLight.Set(new engine.joyce.components.AmbientLight(new Vector4(0.01f, 0.01f, 0.01f, 0.0f)));
        }

        /*
         * Create a scene camera.
         * Keep it invisible.
         */
        {
            _eCamScene = _engine.CreateEntity("RootScene.SceneCamera");
            var cCamScene = new engine.joyce.components.Camera3();
            cCamScene.Angle = 60.0f;
            cCamScene.NearFrustum = 1f;
            cCamScene.CameraFlags = 
                engine.joyce.components.Camera3.Flags.PreloadOnly
                | engine.joyce.components.Camera3.Flags.RenderSkyboxes
                | engine.joyce.components.Camera3.Flags.EnableFog;

            /*
             * We need to be as far away as the skycube is. Plus a bonus.
             */
            cCamScene.FarFrustum = (float)Math.Sqrt(3) * 1000f + 100f;
            cCamScene.Renderbuffer = I.Get<ObjectRegistry<Renderbuffer>>().Get("rootscene_3d");
            cCamScene.CameraMask = 0x00000001;
            _eCamScene.Set(cCamScene);
            I.Get<TransformApi>().SetCameraMask(_eCamScene, 0x00000001);
            I.Get<TransformApi>().SetVisible(_eCamScene, true);
            // No set position, done by controller
        }
        _engine.SetCameraEntity(_eCamScene);

        I.Get<SubscriptionManager>().Subscribe("nogame.scenes.root.Scene.kickoff", _onRootKickoff);
        
        _engine.AddModule(this);
        _engine.OnLogicalFrame += _onLogicalFrame;
    }
}