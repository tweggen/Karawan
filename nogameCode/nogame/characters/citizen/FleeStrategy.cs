using System.Threading;
using engine;
using engine.behave;
using engine.news;
using static engine.Logger;

namespace nogame.characters.citizen;


/**
 * The entity has been frightened and tries to escape from the threat.
 * It keeps fleeing until it is peaceful again.
 *
 * TXWTODO: I would need shared stats about the character at this point
 * to do it properly.
 *
 * TXWTODO: I would need to carry over the previous behavior's state
 * into this behavior. This is what sync was intended to.
 */
public class FleeStrategy : AEntityStrategyPart
{
    public required CharacterModelDescription CharacterModelDescription { get; init; }
    public required CharacterState CharacterState { get; init; }

    /**
     * We need a walk behavior that exceeds our lifetime.
     */
    public required INavigator Navigator { get; init; }
    
    
    public int FleeMilliseconds { get; set; } = 10000;

    private WalkBehavior _walkBehavior;
    
    private Timer _fleeTimer;
    private int _timerGeneration = 0;

    
    /**
     * When a timeout happens, quit the strategy. Fleeing done.
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
    private void _onHitEvent(Event ev)
    {
        Trace($"with entity {_entity.ToString()}");
        lock (this)
        {
            _fleeTimer?.Change(FleeMilliseconds, Timeout.Infinite);
        }
    }

    #region IStrategyPart
    
    /**
     * Remove walk behavior and clean up.
     */
    public override void OnExit()
    {
        lock (this)
        {
            _fleeTimer.Dispose();
            _fleeTimer = null;
        }

        _entity.Remove<engine.behave.components.Behavior>();
        
        var sm = I.Get<SubscriptionManager>();
        sm.Unsubscribe(EntityStrategy.CrashEventPath(_entity), _onHitEvent);
    }

    
    /**
     * Add walk behavior and initialize.
     */
    public override void OnEnter()
    {
        var sm = I.Get<SubscriptionManager>();
        sm.Subscribe(EntityStrategy.HitEventPath(_entity), _onHitEvent);

        /*
         * In the flee state we run.
         */
        Navigator.Speed = CharacterState.BasicSpeed * 2f;
        
        int generation = Interlocked.Increment(ref _timerGeneration);
        lock (this)
        {
            _fleeTimer = new System.Threading.Timer(_onTimeout, generation, FleeMilliseconds, Timeout.Infinite);
        }
        
        _entity.Set(new engine.behave.components.Behavior(_walkBehavior));
    }
    
    #endregion
    
    #region IEntityStrategy
    
    public override void OnDetach(in DefaultEcs.Entity entity)
    {
        _walkBehavior = null;
        base.OnDetach(entity);
    }
    
    
    public override void OnAttach(in engine.Engine engine0, in DefaultEcs.Entity entity0)
    {
        base.OnAttach(engine0, entity0);
        _walkBehavior = new WalkBehavior()
        {
            CharacterModelDescription = CharacterModelDescription, 
            Navigator = Navigator
        };
    }
    
    #endregion
}