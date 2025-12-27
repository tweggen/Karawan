using DefaultEcs;
using engine;
using engine.behave;
using engine.news;

namespace nogame.characters.citizen;


/**
 * Just stay in strategy until a crash happens.
 * Then we can't walk any more.
 *
 * Equip WalkBehavior in the meantime.
 */
public class WalkStrategy : AEntityStrategyPart
{
    /**
     * We need a walk behavior that exceeds our lifetime.
     */
    public required WalkBehavior WalkBehavior { get; init; }
    
    /**
     * If something crashes, I need to give up the driving strategy.
     */
    private void _onCrashEvent(Event ev)
    {
        Controller.GiveUpStrategy(this);
    }

    
    /**
     * Remove walk behavior and clean up.
     */
    public override void OnExit()
    {
        _entity.Remove<engine.behave.components.Behavior>();
        
        var sm = I.Get<SubscriptionManager>();
        sm.Unsubscribe(EntityStrategy.CrashEventPath(_entity), _onCrashEvent);
    }

    
    /**
     * Add walk behavior and initialize.
     */
    public override void OnEnter()
    {
        var sm = I.Get<SubscriptionManager>();
        sm.Subscribe(EntityStrategy.CrashEventPath(_entity), _onCrashEvent);
        
        _entity.Set(new engine.behave.components.Behavior(WalkBehavior));
    }
}