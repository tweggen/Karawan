using System;
using System.Diagnostics;
using System.Numerics;
using DefaultEcs;
using engine;
using engine.behave;
using engine.draw.components;
using engine.draw.systems;
using engine.joyce;
using engine.joyce.components;
using engine.news;
using engine.physics;

namespace nogame.npcs.niceday;

public class NearbyBehavior : ABehavior
{
    private Engine _engine;
    public DefaultEcs.Entity EPOI;
    private DefaultEcs.Entity _eActionMarker;

    private static string _strTalkEvent = "nogame.npcs.niceday.talk";
    
    public float Distance { get; set; } = 16f;


    private void _onTalkNpc(Event ev)
    {
        ev.IsHandled = true;
        
        // TXWTODO: Trigger conversation.
        I.Get<nogame.modules.story.Narration>().TriggerPath("niceguy");
    }

    private void _onInputButton(Event ev)
    {
        if (ev.Code != "<interact>") return;

        _onTalkNpc(ev);
    }


    private float _onInputButtonDistance(Event ev, EmissionContext ectx)
    {
        if (ev.Code != "<interact>") return Single.MinValue;
        
        return (EPOI.Get<engine.joyce.components.Transform3ToWorld>().Matrix.Translation - ectx.PlayerPos).LengthSquared();
    }
    

    private void _detach()
    {
        if (!_eActionMarker.IsAlive) return;
     
        I.Get<SubscriptionManager>().Unsubscribe(_strTalkEvent, _onTalkNpc);
        I.Get<SubscriptionManager>().Unsubscribe(engine.news.Event.INPUT_BUTTON_PRESSED, _onInputButton);
        _eActionMarker.Dispose();
    }
    
    
    public override void OnDetach(in Entity entity)
    {
        _detach();
    }
    
    
    public override void OutOfRange(in Engine engine0, in Entity entity)
    {
        _detach();
    }

    
    public override void InRange(in Engine engine0, in Entity entity)
    {
        if (_eActionMarker.IsAlive) return;

        _engine = engine0;
        _eActionMarker = engine0.CreateEntity("poi.nogame.npcs.nicegui.action");
        _eActionMarker.Set(new OSDText(
            new Vector2(-100f, 0f), new Vector2(200f, 14f), 
            "E to talk", 18, 0xff22aaee,
            0x00000000, engine.draw.HAlign.Center) { MaxDistance = 2f*Distance, CameraMask = 1});
        _eActionMarker.Set(new engine.behave.components.Clickable()
        {
            ClickEventFactory = (e, cev, v2RelPos) => new engine.news.Event(_strTalkEvent, null)
        });
        I.Get<HierarchyApi>().SetParent(_eActionMarker, EPOI);
        I.Get<TransformApi>().SetTransforms(_eActionMarker, true,
            0x00000001, Quaternion.Identity, Vector3.Zero);
        
        I.Get<SubscriptionManager>().Subscribe(engine.news.Event.INPUT_BUTTON_PRESSED, _onInputButton, _onInputButtonDistance);
        I.Get<SubscriptionManager>().Subscribe(_strTalkEvent, _onTalkNpc);
    }
}