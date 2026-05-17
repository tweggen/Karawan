using System;
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
    private static readonly engine.Dc _dc = engine.Dc.TaleEntity;

    private int _npcId;
    private TaleEntityStrategy _strategy;
    private bool _isPaused = false;
    private float _savedSpeed;
    private float _previousSpeed = float.MinValue;
    private Quaternion _prevRotation = Quaternion.Identity;
    private ConversationTimeout _conversationTimeout;

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

        I.Get<SubscriptionManager>().Subscribe(
            EntityStrategy.BumpEventPath(entity0), _onBumpEvent);
    }

    public override void OnDetach(in Entity entity)
    {
        I.Get<SubscriptionManager>().Unsubscribe(
            EntityStrategy.BumpEventPath(entity), _onBumpEvent);
        _disposeConversationTimeout();
        base.OnDetach(entity);
    }

    private void _onBumpEvent(Event ev)
    {
        if (ev is not BumpEvent be) return;
        Navigator?.ApplyLateralBump(be.Direction, 0.4f, 0.4f);
    }

    public override void InRange(in Engine engine0, in Entity entity)
    {
        // Handle Tier 2 → Tier 1 promotion
        if (_strategy != null && _strategy._isTier2)
        {
            Trace(_dc, $"NPC {_npcId} InRange: promoting Tier 2 → Tier 1");
            _strategy.ExitTier2Mode();
            I.Get<TaleManager>().ClearTier2(_npcId);
            return;
        }

        base.InRange(engine0, entity);
    }

    public override void OutOfRange(in Engine engine0, in Entity entity)
    {
        if (_isPaused && _conversationTimeout != null)
        {
            Trace(_dc, $"NPC {_npcId} player walked away, ending conversation");
            _conversationTimeout.CancelNow();
        }
        base.OutOfRange(engine0, entity);
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
        var other = cev.ContactInfo.PropertiesB;

        if (_isPaused && other != null)
        {
            if (CitizenCollisionRouter.IsWeapon(other)) _cancelConversation("hit");
            else if (CitizenCollisionRouter.IsVehicle(other)) _cancelConversation("crash");
        }

        CitizenCollisionRouter.Dispatch(
            cev.ContactInfo.PropertiesA, other, cev.ContactInfo.ContactNormal);
    }

    protected override void OnAction(Event ev)
    {
        ev.IsHandled = true;

        try
        {
            var taleManager = I.Get<TaleManager>();
            var narration = I.Get<Narration>();
            if (taleManager == null || narration == null) return;

            // Stamp NpcSchedule.LastConversationTime. No longer a cooldown gate —
            // the timestamp is a memory signal for future persistence logic
            // (deciding which NPCs survive dematerialization vs. become forgettable).
            // Re-engagement is now bounded only by Narration.MayConverse().
            taleManager.RecordConversation(_npcId);

            var schedule = taleManager.GetSchedule(_npcId);
            if (schedule == null) return;

            var currentStorylet = taleManager.GetCurrentStorylet(_npcId);
            if (currentStorylet == null) return;

            // Inject NPC props and resolve script
            TaleNarrationBindings.InjectNpcProps(schedule);
            string scriptName = TaleNarrationBindings.ResolveScript(currentStorylet, schedule.Role);

            // Stop walking; arm timeout watcher BEFORE triggering so we catch the first NodeReached.
            _pauseWalking(scriptName);

            Trace(_dc, $"NPC {_npcId} paused for conversation, script '{scriptName}'");
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
    /// Called when the watched conversation terminates (player advanced through,
    /// player walked away, terminal-node timeout, or external cancel).
    /// </summary>
    private void _onConversationEnded()
    {
        _conversationTimeout = null;
        if (!_isPaused) return;
        Trace(_dc, $"NPC {_npcId} conversation ended, resuming walk");
        _resumeWalking();
    }

    /// <summary>
    /// Cancel the active conversation (e.g., NPC was hit by a car mid-dialog).
    /// </summary>
    private void _cancelConversation(string reason)
    {
        Trace(_dc, $"NPC {_npcId} conversation cancelled ({reason})");
        if (_conversationTimeout != null)
        {
            _conversationTimeout.CancelNow();
        }
        else
        {
            I.Get<Narration>().CancelConversation();
        }
    }

    private void _pauseWalking(string scriptName)
    {
        _savedSpeed = Navigator.Speed;
        Navigator.Speed = 0;
        _isPaused = true;
        _previousSpeed = float.MinValue; // force animation update to idle

        _disposeConversationTimeout();
        _conversationTimeout = new ConversationTimeout(_engine, scriptName, _onConversationEnded);
    }

    private void _resumeWalking()
    {
        _disposeConversationTimeout();
        Navigator.Speed = _savedSpeed;
        _isPaused = false;
        _previousSpeed = float.MinValue; // force animation update back to walk
    }

    private void _disposeConversationTimeout()
    {
        var w = _conversationTimeout;
        _conversationTimeout = null;
        w?.Dispose();
    }
}
