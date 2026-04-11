using System;
using System.Threading;
using DefaultEcs;
using engine;
using engine.behave;
using static engine.Logger;

namespace nogame.characters.citizen;

/// <summary>
/// Strategy part: hold NPC at current location for a game-time duration.
/// If IsIndoorActivity is true, hides the NPC (no idle animation).
/// Otherwise plays idle animation. Signals GiveUpStrategy when the duration elapses.
/// TaleEntityStrategy sets StayDurationSeconds and IsIndoorActivity before triggering this.
/// </summary>
public class StayAtStrategyPart : AEntityStrategyPart
{
    public required CharacterModelDescription CharacterModelDescription { get; init; }
    public required CharacterState CharacterState { get; init; }

    /// <summary>
    /// Real-time duration in seconds to stay. Set by TaleEntityStrategy
    /// before TriggerStrategy("activity"). This accounts for time dilation
    /// (RealSecondsPerGameDay).
    /// </summary>
    public float StayDurationSeconds { get; set; } = 10f;

    /// <summary>
    /// If true, NPC is indoors and should not display idle animation.
    /// Set by TaleEntityStrategy based on location type.
    /// </summary>
    public bool IsIndoorActivity { get; set; } = false;

    /// <summary>
    /// Optional custom behavior to use instead of IdleBehavior during outdoor activity.
    /// Set by TaleEntityStrategy._setupActivity() for TALE NPCs (e.g. TaleConversationBehavior).
    /// </summary>
    public IBehavior ActivityBehavior { get; set; } = null;

    private IdleBehavior _idleBehavior;
    private Timer _stayTimer;
    private int _timerGeneration = 0;


    private void _onTimeout(object state)
    {
        if (_timerGeneration != (int)state) return;
        _engine.QueueEventHandler(() => Controller.GiveUpStrategy(this));
    }


    public override void OnEnter()
    {
        // Only set up idle behavior if this is an outdoor activity
        if (!IsIndoorActivity)
        {
            if (ActivityBehavior != null)
            {
                Trace($"StayAtStrategyPart: Using ActivityBehavior ({ActivityBehavior.GetType().Name})");
                _entity.Set(new engine.behave.components.Behavior(ActivityBehavior));
            }
            else
            {
                Trace($"StayAtStrategyPart: Using default IdleBehavior");
                _idleBehavior = new IdleBehavior
                {
                    CharacterModelDescription = CharacterModelDescription
                };

                _entity.Set(new engine.behave.components.Behavior(_idleBehavior));
            }
        }
        else
        {
            // Hide indoor NPCs (they're inside buildings)
            Trace($"StayAtStrategyPart: Hiding entity (indoor activity)");
            var transformApi = I.Get<engine.joyce.TransformApi>();
            transformApi.SetVisible(_entity, false, false);
        }

        int milliseconds = (int)(StayDurationSeconds * 1000f);
        if (milliseconds < 100) milliseconds = 100;

        int generation = Interlocked.Increment(ref _timerGeneration);
        lock (this)
        {
            _stayTimer = new Timer(_onTimeout, generation, milliseconds, Timeout.Infinite);
        }
    }


    public override void OnExit()
    {
        lock (this)
        {
            _stayTimer?.Dispose();
            _stayTimer = null;
        }

        if (_entity.IsAlive && _entity.Has<engine.behave.components.Behavior>())
            _entity.Remove<engine.behave.components.Behavior>();

        // Restore visibility for indoor NPCs
        if (IsIndoorActivity && _entity.IsAlive)
        {
            var transformApi = I.Get<engine.joyce.TransformApi>();
            transformApi.SetVisible(_entity, true, false);
        }

        _idleBehavior = null;
    }


    #region IEntityStrategy

    public override void OnDetach(in Entity entity)
    {
        _idleBehavior = null;
        base.OnDetach(entity);
    }


    public override void OnAttach(in engine.Engine engine0, in Entity entity0)
    {
        base.OnAttach(engine0, entity0);
    }

    #endregion
}
