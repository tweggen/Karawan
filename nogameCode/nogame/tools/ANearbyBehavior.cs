using System;
using System.Collections.Generic;
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

    // --- Static registry for best-candidate selection ---
    private static readonly HashSet<ANearbyBehavior> _inRangeSet = new();
    private static readonly object _loRegistry = new();
    private bool _isShowingPrompt = false;
    private static bool _registeredPostUpdate = false;


    public abstract string Prompt { get; }

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


    /// <summary>
    /// Compute a direction-weighted distance score for this behavior.
    /// Lower is better. Combines squared distance with camera-facing penalty.
    /// </summary>
    private float _computeScore(Vector3 cameraPos, Vector3 cameraForward)
    {
        if (!_mayConverse) return Single.MaxValue;
        if (!EPOI.IsAlive || !EPOI.Has<Transform3ToWorld>()) return Single.MaxValue;

        Vector3 npcPos = EPOI.Get<Transform3ToWorld>().Matrix.Translation;
        Vector3 toNpc = npcPos - cameraPos;
        float distSq = toNpc.LengthSquared();

        if (distSq < 0.0001f) return 0f;

        // If camera forward is not available, fall back to pure distance
        if (cameraForward.LengthSquared() < 0.001f)
            return distSq;

        Vector3 toNpcDir = Vector3.Normalize(toNpc);
        Vector3 camFwd = Vector3.Normalize(cameraForward);
        float dot = Vector3.Dot(camFwd, toNpcDir); // 1=in front, -1=behind

        // Multiplier range: 0.5 (directly ahead) to 2.0 (behind)
        float directionMultiplier = 1.25f - 0.75f * dot;

        return distSq * directionMultiplier;
    }


    /// <summary>
    /// Re-evaluate which ANearbyBehavior should show its prompt.
    /// Called once per frame via BehaviorSystem.OnPostUpdate.
    /// Only the best candidate (lowest score) shows its OSD marker.
    /// </summary>
    internal static void UpdateBestCandidate()
    {
        var ectx = I.Get<EmissionContext>();
        if (ectx == null) return;

        lock (_loRegistry)
        {
            ANearbyBehavior best = null;
            float bestScore = Single.MaxValue;

            foreach (var behavior in _inRangeSet)
            {
                float score = behavior._computeScore(ectx.CameraPos, ectx.CameraForward);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = behavior;
                }
            }

            foreach (var behavior in _inRangeSet)
            {
                bool shouldShow = (behavior == best) && behavior._mayConverse;
                if (behavior._isShowingPrompt != shouldShow)
                {
                    behavior._isShowingPrompt = shouldShow;
                    if (behavior._eActionMarker.IsAlive)
                    {
                        I.Get<TransformApi>().SetVisible(behavior._eActionMarker, shouldShow);
                    }
                }
            }
        }
    }


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
     * direction-weighted distance to the NPC.
     */
    private float _onInputButtonDistance(Event ev, EmissionContext ectx)
    {
        if (ev.Code != "<interact>" || !_mayConverse)
        {
            return Single.MaxValue;
        }

        return _computeScore(ectx.CameraPos, ectx.CameraForward);
    }


    /**
     * When the narration system signals a state change, adapt the visibility of the
     * action marker of the NPC.
     */
    private void _onNarrationStateChanged(Event ev)
    {
        var csev = ev as engine.narration.NarrationStateEvent;
        lock (_lo)
        {
            if (_mayConverse == csev.MayConverse)
            {
                return;
            }
            _mayConverse = csev.MayConverse;
        }
        // Visibility is handled by UpdateBestCandidate each frame
    }


    private void _detach()
    {
        lock (_loRegistry)
        {
            _inRangeSet.Remove(this);
            _isShowingPrompt = false;
        }

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
            // Already created; visibility managed by UpdateBestCandidate
            return;
        }

        _mayConverse = I.Get<Narration>().MayConverse();
        _eActionMarker = engine0.CreateEntity(EntityName);
        _eActionMarker.Set(new OSDText(
            new Vector2(-100f, 0f), new Vector2(200f, 14f),
            Prompt, 18, 0xff22aaee,
            0x00000000, engine.draw.HAlign.Center) { MaxDistance = 2f*Distance, CameraMask = 1});
        _eActionMarker.Set(new engine.behave.components.Clickable()
        {
            ClickEventFactory = (e, cev, v2RelPos) => new engine.news.Event(ActionEvent, null)
        });
        I.Get<HierarchyApi>().SetParent(_eActionMarker, EPOI);
        // Start hidden; UpdateBestCandidate will show the winning one
        I.Get<TransformApi>().SetTransforms(_eActionMarker, false,
            0x00000001, Quaternion.Identity, Vector3.Zero);
        _isShowingPrompt = false;

        var sm = I.Get<SubscriptionManager>();
        sm.Subscribe(engine.news.Event.INPUT_BUTTON_PRESSED, _onInputButton, _onInputButtonDistance);
        sm.Subscribe(ActionEvent, OnAction);
        sm.Subscribe(nogame.modules.story.Narration.EventTypeCurrentState, _onNarrationStateChanged);

        lock (_loRegistry)
        {
            _inRangeSet.Add(this);
        }
    }


    public override void OnAttach(in Engine engine0, in Entity entity0)
    {
        _engine = engine0;
        _eTarget = entity0;

        // Register the per-frame callback once
        if (!_registeredPostUpdate)
        {
            _registeredPostUpdate = true;
            engine0.OnBehaviorPostUpdate += _ => UpdateBestCandidate();
        }

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
