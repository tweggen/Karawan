using System;
using System.Numerics;
using DefaultEcs;
using engine;
using engine.behave;
using engine.draw.components;
using engine.joyce;
using engine.news;
using builtin.modules.story;

namespace nogame.npcs.niceday;

public class NearbyBehavior : ABehavior
{
    private object _lo = new();
    
    private Engine _engine;
    private DefaultEcs.Entity _eTarget;
    public DefaultEcs.Entity EPOI;
    private DefaultEcs.Entity _eActionMarker;
    private bool _mayConverse = true;

    private static string _strTalkEvent = "nogame.npcs.niceday.talk";
    
    public float Distance { get; set; } = 16f;


    private void _onTalkNpc(Event ev)
    {
        ev.IsHandled = true;
        
        // TXWTODO: Trigger conversation.
        I.Get<builtin.modules.story.Narration>().TriggerConversation("niceguy", _eTarget.ToString());
    }

    
    /**
     * An actual interaction event has been passed to the npc.
     */
    private void _onInputButton(Event ev)
    {
        if (!_mayConverse) return;
        if (ev.Code != "<interact>") return;

        _onTalkNpc(ev);
    }


    /**
     * When considering an event to be passed to this handler, consider the
     * distance to the NPC.
     */
    private float _onInputButtonDistance(Event ev, EmissionContext ectx)
    {
        if (
            false
            || ev.Code != "<interact>"
            || !_mayConverse
        )
        {
            return Single.MaxValue;
        }
        
        return (EPOI.Get<engine.joyce.components.Transform3ToWorld>().Matrix.Translation - ectx.PlayerPos).LengthSquared();
    }


    /**
     * When the narration system signals a state change, adapt the visibility of the
     * action marker of the NPC.
     */
    private void _onNarrationStateChanged(Event ev)
    {
        var csev = ev as nogame.modules.story.CurrentStateEvent;
        lock (_lo)
        {
            if (_mayConverse == csev.MayConverse)
            {
                return;
            }
            _mayConverse = csev.MayConverse;
        }
        _engine.RunMainThread(() =>
        {
            I.Get<TransformApi>().SetVisible(_eActionMarker, csev.MayConverse);
        });
    }
    

    private void _detach()
    {
        if (!_eActionMarker.IsAlive) return;
     
        var sm = I.Get<SubscriptionManager>();
        sm.Unsubscribe(nogame.modules.story.Narration.EventTypeCurrentState, _onNarrationStateChanged);
        sm.Unsubscribe(_strTalkEvent, _onTalkNpc);
        sm.Unsubscribe(engine.news.Event.INPUT_BUTTON_PRESSED, _onInputButton);
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
        if (_eActionMarker.IsAlive)
        {
            I.Get<TransformApi>().SetVisible(_eActionMarker, _mayConverse);
            return;
        }

        _engine = engine0;
        _eTarget = entity;
        _mayConverse = I.Get<Narration>().MayConverse();
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
        I.Get<TransformApi>().SetTransforms(_eActionMarker, _mayConverse,
            0x00000001, Quaternion.Identity, Vector3.Zero);
        
        var sm = I.Get<SubscriptionManager>();
        sm.Subscribe(engine.news.Event.INPUT_BUTTON_PRESSED, _onInputButton, _onInputButtonDistance);
        sm.Subscribe(_strTalkEvent, _onTalkNpc);
        sm.Subscribe(nogame.modules.story.Narration.EventTypeCurrentState, _onNarrationStateChanged);
    }
}