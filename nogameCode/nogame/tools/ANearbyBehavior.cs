using System;
using System.Numerics;
using DefaultEcs;
using engine;
using engine.behave;
using engine.draw.components;
using engine.joyce;
using engine.joyce.components;
using engine.news;
using nogame.modules.story;

namespace nogame.tools;

public abstract class ANearbyBehavior : ABehavior
{
    public PositionDescription? PositionDescription = null;
    
    protected object _lo = new();
    protected Engine _engine;
    protected DefaultEcs.Entity _eTarget;
    public DefaultEcs.Entity EPOI;
    private DefaultEcs.Entity _eActionMarker;
    private bool _mayConverse = true;

    public abstract string Name { get; }
    public string ActionEvent
    {
        get => $"{Name}.action";
    }
    
    public string EntityName
    {
        get => $"{Name}";
    }
    
    public virtual float Distance { get; set; } = 16f;


    protected abstract void OnAction(Event ev);

    
    /**
     * An actual interaction event has been passed to the npc.
     */
    private void _onInputButton(Event ev)
    {
        if (!_mayConverse) return;
        if (ev.Code != "<interact>") return;

        OnAction(ev);
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
        sm.Unsubscribe(ActionEvent, OnAction);
        sm.Unsubscribe(engine.news.Event.INPUT_BUTTON_PRESSED, _onInputButton);
        _eActionMarker.Dispose();
    }


    public override void Sync(in Entity entity)
    {
        int a = 1;
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

        _mayConverse = I.Get<Narration>().MayConverse();
        _eActionMarker = engine0.CreateEntity("poi.nogame.npcs.nicegui.action");
        _eActionMarker.Set(new OSDText(
            new Vector2(-100f, 0f), new Vector2(200f, 14f), 
            "E to talk", 18, 0xff22aaee,
            0x00000000, engine.draw.HAlign.Center) { MaxDistance = 2f*Distance, CameraMask = 1});
        _eActionMarker.Set(new engine.behave.components.Clickable()
        {
            ClickEventFactory = (e, cev, v2RelPos) => new engine.news.Event(ActionEvent, null)
        });
        I.Get<HierarchyApi>().SetParent(_eActionMarker, EPOI);
        I.Get<TransformApi>().SetTransforms(_eActionMarker, _mayConverse,
            0x00000001, Quaternion.Identity, Vector3.Zero);
        
        var sm = I.Get<SubscriptionManager>();
        sm.Subscribe(engine.news.Event.INPUT_BUTTON_PRESSED, _onInputButton, _onInputButtonDistance);
        sm.Subscribe(ActionEvent, OnAction);
        sm.Subscribe(nogame.modules.story.Narration.EventTypeCurrentState, _onNarrationStateChanged);
    }


    public override void OnAttach(in Engine engine0, in Entity entity0)
    {
        _engine = engine0;
        _eTarget = entity0;

        /*
         * If we have a position description, place ourselves. 
         */
        if (null != PositionDescription)
        {
            _eTarget.Set(new engine.joyce.components.Transform3ToWorld(
                0x00000001, 
                Transform3ToWorld.Visible,
                Matrix4x4.CreateFromQuaternion(PositionDescription.Orientation)
                *Matrix4x4.CreateTranslation(PositionDescription.Position)));
            _eTarget.Set(new engine.joyce.components.Transform3(true, 0x00000001, 
                PositionDescription.Orientation, PositionDescription.Position));
        }
    }
}