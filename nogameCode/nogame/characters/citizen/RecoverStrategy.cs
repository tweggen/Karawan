using System;
using System.Threading;
using engine;
using engine.behave;
using engine.news;
using static engine.Logger;

namespace nogame.characters.citizen;

/**
 * Stay in recover for 5sec (RecoverMilliseconds) without
 * any collision.
 *
 * Equip AfterCrashBehavior during that time.
 */
public class RecoverStrategy : AEntityStrategyPart
{
    public required CharacterModelDescription CharacterModelDescription { get; init; }
    public required CharacterState CharacterState { get; init; }

    public int RecoverMilliseconds { get; set; } = 5000;
    
    private RecoverBehavior _recoverBehavior;

    private Timer _recoverTimer;
    private int _timerGeneration = 0;


    /**
     * When a timeout happens, quit the strategy.
     */
    private void _onTimeout(object state)
    {
        if (_timerGeneration != (int)state) return;
        // TXWTODO: Frankly, I do not want to think about threading in strategies. Whichever way it may work.
        _engine.QueueEventHandler(() => Controller.GiveUpStrategy(this));
    }
    
    
    /**
     * Whenever a crash happens, retrigger the timer that
     * tracks our recovery.
     */
    private void _onCrashEvent(Event ev)
    {
        Trace($"with entity {_entity.ToString()}");
        lock (this)
        {
            _recoverTimer?.Change(RecoverMilliseconds, Timeout.Infinite);
        }
    }

    #region IEntityStrategy
    
    public override void OnDetach(in DefaultEcs.Entity entity)
    {
        _recoverBehavior = null;
        base.OnDetach(entity);
    }
    
    
    public override void OnAttach(in engine.Engine engine0, in DefaultEcs.Entity entity0)
    {
        base.OnAttach(engine0, entity0);
        _recoverBehavior = new RecoverBehavior()
        {
            CharacterModelDescription = CharacterModelDescription
        };
    }
    
    #endregion
    
    #region IStrategyPart
    
    /**
     * Remove crash behavior, clean up.
     */
    public override void OnExit()
    {
        lock (this)
        {
            _recoverTimer.Dispose();
            _recoverTimer = null;
        }

        _entity.Remove<engine.behave.components.Behavior>();
        
        var sm = I.Get<SubscriptionManager>();
        sm.Unsubscribe(EntityStrategy.CrashEventPath(_entity), _onCrashEvent);
    }

    
    /**
     * Create and attach recover behavior, initalize.
     */
    public override void OnEnter()
    {    
        var sm = I.Get<SubscriptionManager>();
        sm.Subscribe(EntityStrategy.CrashEventPath(_entity), _onCrashEvent);
        
        int generation = Interlocked.Increment(ref _timerGeneration);
        lock (this)
        {
            _recoverTimer = new System.Threading.Timer(_onTimeout, generation, RecoverMilliseconds, Timeout.Infinite);
        }

        _entity.Set(new engine.behave.components.Behavior(_recoverBehavior));
    }
    
    #endregion
}