using System;
using System.Collections.Generic;
using System.Diagnostics;
using engine;
using engine.joyce;
using System.Numerics;
using engine.behave.systems;
using engine.news;
using static engine.Logger;

namespace nogame.modules.osd;


public class Camera : AModule
{
    private TransformApi _aTransform = I.Get<TransformApi>();
    
    private DefaultEcs.Entity _eCamOSD;

    public override IEnumerable<IModuleDependency> ModuleDepends()
    {
        return new List<IModuleDependency>()
        {
            new MyModule<engine.news.ClickModule>() { Activate = false } 
        };
    }

    public override void ModuleDeactivate()
    {
        var clickModule = M<ClickModule>();
        clickModule.ModuleDeactivate();
        
        _engine.RemoveModule(this);
        
        _eCamOSD.Dispose();
        base.ModuleDeactivate();
    }


    public override void ModuleActivate(Engine engine0)
    {
        base.ModuleActivate(engine0);
        _engine.AddModule(this);

        /*
         * Create an osd camera
         */
        {
            _eCamOSD = _engine.CreateEntity("RootScene.OSDCamera");
            var cCamOSD = new engine.joyce.components.Camera3();
            cCamOSD.Angle = 0f;
            cCamOSD.NearFrustum = 1 / Single.Tan(30f * Single.Pi / 180f);
            cCamOSD.FarFrustum = 100f;
            cCamOSD.CameraMask = 0x01000000;
            cCamOSD.CameraFlags =
                engine.joyce.components.Camera3.Flags.PreloadOnly
                | engine.joyce.components.Camera3.Flags.DisableDepthTest;
            _eCamOSD.Set(cCamOSD);
            _aTransform.SetTransforms(_eCamOSD,
                true, 0x01000000,
                Quaternion.Identity, new Vector3(0f, 0f, 14f));
            _eCamOSD.Get<engine.joyce.components.Camera3>().CameraFlags &=
                ~engine.joyce.components.Camera3.Flags.PreloadOnly;
        }

        var clickModule = M<ClickModule>();
        clickModule.Camera = _eCamOSD;
        clickModule.ModuleActivate(engine0);
    }
}