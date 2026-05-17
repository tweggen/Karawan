using System;
using builtin.tools;
using DefaultEcs;
using engine;
using engine.behave.components;
using engine.joyce.components;
using engine.news;
using engine.physics;
using engine.tale;
using nogame.modules.story;
using nogame.modules.tale;
using nogame.tools;
using static engine.Logger;

namespace nogame.characters.citizen;

/// <summary>
/// Behavior for TALE NPCs: allows player to initiate conversations via E key.
/// Extends ANearbyBehavior to handle "E to Talk" prompt and interaction.
/// Attaches/detaches based on activity phase and indoor/outdoor status.
/// </summary>
public class TaleConversationBehavior : ANearbyBehavior
{
    private static readonly engine.Dc _dc = engine.Dc.TaleEntity;

    private TaleManager _taleManager;
    private Narration _narration;
    private int _npcId;
    private bool _animationSet = false;
    private TaleEntityStrategy _strategy;
    private ConversationTimeout _conversationTimeout;

    public CharacterModelDescription CharacterModelDescription;

    public override string Prompt => "E to Talk";

    public override string Name => "TaleConversation";

    public override float Distance { get; set; } = 12f;

    public TaleConversationBehavior(int npcId, TaleEntityStrategy strategy = null)
    {
        _npcId = npcId;
        _strategy = strategy;
    }

    public override void InRange(in engine.Engine engine0, in Entity entity)
    {
        // Handle Tier 2 → Tier 1 promotion (replaces TaleEntityBehavior)
        if (_strategy != null && _strategy._isTier2)
        {
            Trace(_dc, $"NPC {_npcId} InRange: promoting Tier 2 → Tier 1");
            _strategy.ExitTier2Mode();
            I.Get<TaleManager>().ClearTier2(_npcId);
            return;
        }

        bool mayConverse = I.Get<Narration>().MayConverse();
        Trace(_dc, $"NPC {_npcId} InRange: mayConverse={mayConverse}");
        base.InRange(engine0, entity);
    }

    public override void OutOfRange(in engine.Engine engine0, in Entity entity)
    {
        if (_conversationTimeout != null)
        {
            Trace(_dc, $"NPC {_npcId} player walked away, ending conversation");
            _conversationTimeout.CancelNow();
        }
        base.OutOfRange(engine0, entity);
    }

    public override void OnDetach(in Entity entity)
    {
        _disposeConversationTimeout();
        base.OnDetach(entity);
    }

    public override void OnAttach(in engine.Engine engine0, in Entity entity0)
    {
        base.OnAttach(engine0, entity0);
        EPOI = entity0;
        _animationSet = false;
        Trace(_dc, $"NPC {_npcId} TaleConversationBehavior.OnAttach called");

        if (entity0.Has<engine.physics.components.Body>())
        {
            ref var cBody = ref entity0.Get<engine.physics.components.Body>();
            cBody.PhysicsObject?.MakeKinematic(ref cBody.Reference);
        }
    }

    public override void Behave(in Entity entity, float dt)
    {
        if (!entity.IsAlive) return;

        if (!_animationSet)
        {
            _animationSet = true;
            if (entity.Has<GPUAnimationState>() && entity.Has<FromModel>())
            {
                ref var cGpuAnimationState = ref entity.Get<GPUAnimationState>();
                ref var cFromModel = ref entity.Get<FromModel>();
                ref var model = ref cFromModel.Model;
                cGpuAnimationState.AnimationState?.SetAnimation(
                    model, CharacterModelDescription.IdleAnimName, 0);
            }
        }
    }

    public override void OnCollision(ContactEvent cev)
    {
        base.OnCollision(cev);
        var other = cev.ContactInfo.PropertiesB;

        if (other != null)
        {
            if (CitizenCollisionRouter.IsWeapon(other)) _cancelIfConversing("hit");
            else if (CitizenCollisionRouter.IsVehicle(other)) _cancelIfConversing("crash");
        }

        CitizenCollisionRouter.Dispatch(
            cev.ContactInfo.PropertiesA, other, cev.ContactInfo.ContactNormal);
    }

    private void _cancelIfConversing(string reason)
    {
        var narration = I.Get<Narration>();
        if (narration != null && !narration.MayConverse())
        {
            Trace(_dc, $"NPC {_npcId} conversation cancelled ({reason})");
            if (_conversationTimeout != null)
            {
                _conversationTimeout.CancelNow();
            }
            else
            {
                narration.CancelConversation();
            }
        }
    }

    private void _onConversationEnded()
    {
        _conversationTimeout = null;
    }

    private void _disposeConversationTimeout()
    {
        var w = _conversationTimeout;
        _conversationTimeout = null;
        w?.Dispose();
    }

    protected override void OnAction(Event ev)
    {
        ev.IsHandled = true;

        try
        {
            _taleManager = I.Get<TaleManager>();
            _narration = I.Get<Narration>();

            if (_taleManager == null || _narration == null)
            {
                Trace(_dc, $"TaleManager or Narration not available");
                return;
            }

            // Stamp NpcSchedule.LastConversationTime. No longer a cooldown gate —
            // the timestamp is a memory signal for future persistence logic
            // (deciding which NPCs survive dematerialization vs. become forgettable).
            // Re-engagement is now bounded only by Narration.MayConverse().
            _taleManager.RecordConversation(_npcId);

            var schedule = _taleManager.GetSchedule(_npcId);
            if (schedule == null)
            {
                Trace(_dc, $"NPC {_npcId} schedule not found");
                return;
            }

            // Get current storylet to determine conversation script
            var currentStorylet = _taleManager.GetCurrentStorylet(_npcId);
            if (currentStorylet == null)
            {
                Trace(_dc, $"NPC {_npcId} has no current storylet");
                return;
            }

            // Inject TALE properties into narration Props
            TaleNarrationBindings.InjectNpcProps(schedule);

            // Resolve conversation script using 5-level fallback
            string scriptName = TaleNarrationBindings.ResolveScript(currentStorylet, schedule.Role);

            Trace(_dc, $"NPC {_npcId} ({schedule.Role}) triggered conversation, using script '{scriptName}'");

            // Arm timeout watcher BEFORE triggering so we catch the first NodeReached.
            _disposeConversationTimeout();
            _conversationTimeout = new ConversationTimeout(_engine, scriptName, _onConversationEnded);

            // Trigger conversation in narration system
            _narration.TriggerConversation(scriptName, _npcId.ToString());
        }
        catch (Exception e)
        {
            Error($"TALE CONVERSATION: Exception in OnAction: {e.Message}\n{e.StackTrace}");
        }
    }
}
