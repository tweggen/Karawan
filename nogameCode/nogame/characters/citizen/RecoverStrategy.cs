using System;
using System.Threading;
using engine;
using engine.behave;
using engine.news;

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
    public int RecoverMilliseconds { get; set; } = 5000;
    
    private AfterCrashBehavior _afterCrashBehavior;

    private Timer _recoverTimer;
    private int _timerGeneration = 0;


    /**
     * When a timeout happens, quit the strategy.
     */
    private void _onTimeout(object state)
    {
        if (_timerGeneration != (int)state) return;
        
        Controller.GiveUpStrategy(this);
    }
    
    
    /**
     * Whenever a crash happens, retrigger the timer that
     * tracks our recovery.
     */
    private void _onCrashEvent(Event ev)
    {
        lock (this)
        {
            _recoverTimer?.Change(RecoverMilliseconds, Timeout.Infinite);
        }
    }

    
    public override void OnDetach(in DefaultEcs.Entity entity)
    {
        _afterCrashBehavior = null;
    }
    
    
    public override void OnAttach(in engine.Engine engine0, in DefaultEcs.Entity entity0)
    {
        _afterCrashBehavior = new AfterCrashBehavior() { CharacterModelDescription = CharacterModelDescription };
    }
    
    
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

        _entity.Set(new engine.behave.components.Behavior(_afterCrashBehavior));
    }
}