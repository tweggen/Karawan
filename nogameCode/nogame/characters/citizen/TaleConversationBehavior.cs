using System;
using System.Collections.Generic;
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
    private TaleManager _taleManager;
    private Narration _narration;
    private int _npcId;
    private bool _animationSet = false;
    private TaleEntityStrategy _strategy;

    public CharacterModelDescription CharacterModelDescription;

    // Phase C4: Cooldown to suppress repeated conversations with the same NPC
    // Shared with TaleWalkBehavior via static accessors
    private static readonly Dictionary<int, DateTime> _lastConversationTime = new();
    private const int CooldownSeconds = 30;

    public static bool IsOnCooldown(int npcId)
    {
        return _lastConversationTime.TryGetValue(npcId, out var lastTime)
            && (DateTime.UtcNow - lastTime).TotalSeconds < CooldownSeconds;
    }

    public static void SetCooldown(int npcId)
    {
        _lastConversationTime[npcId] = DateTime.UtcNow;
    }

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
            Trace($"TALE CONVERSATION: NPC {_npcId} InRange: promoting Tier 2 → Tier 1");
            _strategy.ExitTier2Mode();
            I.Get<TaleManager>().ClearTier2(_npcId);
            return;
        }

        bool mayConverse = I.Get<Narration>().MayConverse();
        Trace($"TALE CONVERSATION: NPC {_npcId} InRange: mayConverse={mayConverse}");
        base.InRange(engine0, entity);
    }

    public override void OnAttach(in engine.Engine engine0, in Entity entity0)
    {
        base.OnAttach(engine0, entity0);
        EPOI = entity0;
        _animationSet = false;
        Trace($"TALE CONVERSATION: NPC {_npcId} TaleConversationBehavior.OnAttach called");

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
        var me = cev.ContactInfo.PropertiesA;
        var other = cev.ContactInfo.PropertiesB;

        if (other != null)
        {
            if (0 != (other.SolidLayerMask & CollisionProperties.Layers.AnyWeapon))
            {
                I.Get<engine.news.EventQueue>().Push(
                    new engine.news.Event(EntityStrategy.HitEventPath(me.Entity), ""));
            }
            if (0 != (other.SolidLayerMask & CollisionProperties.Layers.AnyVehicle))
            {
                I.Get<engine.news.EventQueue>().Push(
                    new engine.news.Event(EntityStrategy.CrashEventPath(me.Entity), ""));
            }
        }
    }

    protected override void OnAction(Event ev)
    {
        try
        {
            // Phase C4: Cooldown - suppress repeated conversations with the same NPC
            if (_lastConversationTime.TryGetValue(_npcId, out var lastTime)
                && (DateTime.UtcNow - lastTime).TotalSeconds < CooldownSeconds)
            {
                Trace($"TALE CONVERSATION: NPC {_npcId} on cooldown");
                return;
            }
            _lastConversationTime[_npcId] = DateTime.UtcNow;

            _taleManager = I.Get<TaleManager>();
            _narration = I.Get<Narration>();

            if (_taleManager == null || _narration == null)
            {
                Trace($"TALE CONVERSATION: TaleManager or Narration not available");
                return;
            }

            var schedule = _taleManager.GetSchedule(_npcId);
            if (schedule == null)
            {
                Trace($"TALE CONVERSATION: NPC {_npcId} schedule not found");
                return;
            }

            // Get current storylet to determine conversation script
            var currentStorylet = _taleManager.GetCurrentStorylet(_npcId);
            if (currentStorylet == null)
            {
                Trace($"TALE CONVERSATION: NPC {_npcId} has no current storylet");
                return;
            }

            // Inject TALE properties into narration Props
            TaleNarrationBindings.InjectNpcProps(schedule);

            // Resolve conversation script using 5-level fallback
            string scriptName = TaleNarrationBindings.ResolveScript(currentStorylet, schedule.Role);

            Trace($"TALE CONVERSATION: NPC {_npcId} ({schedule.Role}) triggered conversation, using script '{scriptName}'");

            // Trigger conversation in narration system
            _narration.TriggerConversation(scriptName, _npcId.ToString());
        }
        catch (Exception e)
        {
            Error($"TALE CONVERSATION: Exception in OnAction: {e.Message}\n{e.StackTrace}");
        }
    }
}
