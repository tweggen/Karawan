using System;
using System.Threading;
using DefaultEcs;
using engine;
using engine.behave;
using static engine.Logger;

namespace nogame.characters.citizen;

/// <summary>
/// Strategy part: hold NPC at current location for a game-time duration.
/// Plays idle animation. Signals GiveUpStrategy when the duration elapses.
/// TaleEntityStrategy sets StayDurationSeconds before triggering this.
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
        _idleBehavior = new IdleBehavior
        {
            CharacterModelDescription = CharacterModelDescription
        };

        _entity.Set(new engine.behave.components.Behavior(_idleBehavior));

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
