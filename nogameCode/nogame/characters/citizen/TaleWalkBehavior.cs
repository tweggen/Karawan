using System;
using System.Collections.Generic;
using System.Numerics;
using builtin.tools;
using DefaultEcs;
using engine;
using engine.joyce.components;
using engine.narration;
using engine.news;
using engine.physics;
using engine.tale;
using nogame.modules.story;
using nogame.modules.tale;
using nogame.tools;
using static engine.Logger;

namespace nogame.characters.citizen;

/// <summary>
/// Walking behavior for TALE NPCs that also shows "E to Talk" prompt.
/// Extends ANearbyBehavior for proximity/interaction, and delegates to
/// a SegmentNavigator for walking. When the player presses E, the NPC
/// stops walking, triggers a conversation, then resumes when ScriptEndedEvent fires.
/// </summary>
public class TaleWalkBehavior : ANearbyBehavior
{
    private int _npcId;
    private TaleEntityStrategy _strategy;
    private bool _isPaused = false;
    private float _savedSpeed;
    private float _previousSpeed = float.MinValue;
    private Quaternion _prevRotation = Quaternion.Identity;

    public CharacterModelDescription CharacterModelDescription;
    public SegmentNavigator Navigator;

    public override string Prompt => "E to Talk";
    public override string Name => "TaleWalkConversation";
    public override float Distance { get; set; } = 12f;

    public TaleWalkBehavior(int npcId, TaleEntityStrategy strategy)
    {
        _npcId = npcId;
        _strategy = strategy;
    }

    public override void OnAttach(in Engine engine0, in Entity entity0)
    {
        base.OnAttach(engine0, entity0);
        EPOI = entity0;
        _previousSpeed = float.MinValue;
        _isPaused = false;

        Navigator?.NavigatorLoad();

        if (entity0.Has<engine.physics.components.Body>())
        {
            ref var cBody = ref entity0.Get<engine.physics.components.Body>();
            cBody.PhysicsObject?.MakeKinematic(ref cBody.Reference);
        }
    }

    public override void OnDetach(in Entity entity)
    {
        _unsubscribeScriptEnded();
        base.OnDetach(entity);
    }

    public override void InRange(in Engine engine0, in Entity entity)
    {
        // Handle Tier 2 → Tier 1 promotion
        if (_strategy != null && _strategy._isTier2)
        {
            Trace($"TALE WALK: NPC {_npcId} InRange: promoting Tier 2 → Tier 1");
            _strategy.ExitTier2Mode();
            I.Get<TaleManager>().ClearTier2(_npcId);
            return;
        }

        base.InRange(engine0, entity);
    }

    public override void Behave(in Entity entity, float dt)
    {
        if (!entity.IsAlive) return;

        // Navigate (speed=0 when paused → no movement)
        Navigator.NavigatorBehave(dt);
        Navigator.NavigatorGetTransformation(out var vPosition, out var qOrientation);

        qOrientation = Quaternion.Slerp(_prevRotation, qOrientation, 0.1f);
        _prevRotation = qOrientation;
        I.Get<engine.joyce.TransformApi>().SetTransforms(
            entity, true, 0x0000ffff, qOrientation, vPosition);

        // Update animation based on speed
        _updateAnimation(entity);
    }

    private void _updateAnimation(in Entity entity)
    {
        float speed = Navigator.Speed;
        if (speed == _previousSpeed) return;

        if (!entity.Has<GPUAnimationState>()) return;

        ref var cGpuAnimationState = ref entity.Get<GPUAnimationState>();
        ref var cFromModel = ref entity.Get<FromModel>();
        ref var model = ref cFromModel.Model;

        string strAnimation;
        if (speed > 7f / 3.6f)
            strAnimation = CharacterModelDescription.RunAnimName;
        else if (speed > 0f)
            strAnimation = CharacterModelDescription.WalkAnimName;
        else
            strAnimation = CharacterModelDescription.IdleAnimName;

        _previousSpeed = speed;
        cGpuAnimationState.AnimationState?.SetAnimation(model, strAnimation, 0);
    }

    public override void OnCollision(ContactEvent cev)
    {
        base.OnCollision(cev);
        var me = cev.ContactInfo.PropertiesA;
        var other = cev.ContactInfo.PropertiesB;

        if (other != null)
        {
            if (0 != (other.SolidLayerMask & CollisionProperties.Layers.AnyWeapon))
            {
                if (_isPaused) _cancelConversation("hit");
                I.Get<engine.news.EventQueue>().Push(
                    new Event(EntityStrategy.HitEventPath(me.Entity), ""));
            }
            if (0 != (other.SolidLayerMask & CollisionProperties.Layers.AnyVehicle))
            {
                if (_isPaused) _cancelConversation("crash");
                I.Get<engine.news.EventQueue>().Push(
                    new Event(EntityStrategy.CrashEventPath(me.Entity), ""));
            }
        }
    }

    protected override void OnAction(Event ev)
    {
        ev.IsHandled = true;

        try
        {
            // Reuse TaleConversationBehavior's cooldown
            if (TaleConversationBehavior.IsOnCooldown(_npcId))
            {
                Trace($"TALE WALK: NPC {_npcId} on cooldown");
                return;
            }
            TaleConversationBehavior.SetCooldown(_npcId);

            var taleManager = I.Get<TaleManager>();
            var narration = I.Get<Narration>();
            if (taleManager == null || narration == null) return;

            var schedule = taleManager.GetSchedule(_npcId);
            if (schedule == null) return;

            var currentStorylet = taleManager.GetCurrentStorylet(_npcId);
            if (currentStorylet == null) return;

            // Stop walking and subscribe to script-ended event
            _pauseWalking();

            // Inject NPC props and resolve script
            TaleNarrationBindings.InjectNpcProps(schedule);
            string scriptName = TaleNarrationBindings.ResolveScript(currentStorylet, schedule.Role);

            Trace($"TALE WALK: NPC {_npcId} paused for conversation, script '{scriptName}'");
            narration.TriggerConversation(scriptName, _npcId.ToString());
        }
        catch (Exception e)
        {
            Error($"TALE WALK: Exception in OnAction: {e.Message}\n{e.StackTrace}");
            // On error, resume walking so the NPC isn't stuck
            if (_isPaused) _resumeWalking();
        }
    }

    /// <summary>
    /// Called when the narration script ends. This is the signal to resume walking.
    /// </summary>
    private void _onScriptEnded(Event ev)
    {
        if (!_isPaused) return;
        Trace($"TALE WALK: NPC {_npcId} conversation ended, resuming walk");
        _resumeWalking();
    }

    /// <summary>
    /// Cancel the active conversation (e.g., NPC was hit by a car mid-dialog).
    /// </summary>
    private void _cancelConversation(string reason)
    {
        Trace($"TALE WALK: NPC {_npcId} conversation cancelled ({reason})");
        I.Get<Narration>().CancelConversation();
        // _onScriptEnded will fire from the cancel → resumes walking
    }

    private void _pauseWalking()
    {
        _savedSpeed = Navigator.Speed;
        Navigator.Speed = 0;
        _isPaused = true;
        _previousSpeed = float.MinValue; // force animation update to idle

        // Subscribe to script-ended event to know when to resume
        I.Get<SubscriptionManager>().Subscribe(ScriptEndedEvent.EVENT_TYPE, _onScriptEnded);
    }

    private void _resumeWalking()
    {
        _unsubscribeScriptEnded();
        Navigator.Speed = _savedSpeed;
        _isPaused = false;
        _previousSpeed = float.MinValue; // force animation update back to walk
    }

    private void _unsubscribeScriptEnded()
    {
        if (_isPaused)
        {
            I.Get<SubscriptionManager>().Unsubscribe(ScriptEndedEvent.EVENT_TYPE, _onScriptEnded);
        }
    }
}
