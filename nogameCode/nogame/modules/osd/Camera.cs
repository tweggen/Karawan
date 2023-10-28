using System;
using engine;
using engine.joyce;
using System.Numerics;
using engine.behave.systems;
using engine.news;

namespace nogame.modules.osd;

public class Camera : AModule
{
    private TransformApi _aTransform = I.Get<TransformApi>();
    
    private DefaultEcs.Entity _eCamOSD;
    
    private ClickableHandler _clickableHandler;


    private void _testClickable(Event ev)
    {
        var eFound = _clickableHandler.OnClick(ev);
    }


    private void _onTouchPress(Event ev)
    {
        if (GlobalSettings.Get("Android") == "true")
        {
            _testClickable(ev);
        }
    }


    private void _onMousePress(Event ev)
    {
        _testClickable(ev);
    }


    public override void ModuleDeactivate()
    {
        I.Get<SubscriptionManager>().Unsubscribe(Event.INPUT_TOUCH_PRESSED, _onTouchPress);

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
            cCamOSD.CameraFlags = engine.joyce.components.Camera3.Flags.PreloadOnly;
            _eCamOSD.Set(cCamOSD);
            _aTransform.SetPosition(_eCamOSD, new Vector3(0f, 0f, 14f));
            
            _eCamOSD.Get<engine.joyce.components.Camera3>().CameraFlags &=
                ~engine.joyce.components.Camera3.Flags.PreloadOnly;
        }
        
        /*
         * Setup osd interaction handler
         */
        {
            _clickableHandler = new(_engine, _eCamOSD);
        }
        
        I.Get<SubscriptionManager>().Subscribe(Event.INPUT_TOUCH_PRESSED, _onTouchPress);
    }
}