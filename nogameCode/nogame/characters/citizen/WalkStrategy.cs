using DefaultEcs;
using engine;
using engine.behave;
using engine.news;

namespace nogame.characters.citizen;

public class WalkStrategy : AEntityStrategyPart
{
    /**
     * This way we communiate crash events.
     */
    /**
     * If something crashes, I need to give up the driving strategy.
     */
    private void _onCrashEvent(Event ev)
    {
        Controller.GiveUpStrategy(this);
    }

    
    public override void OnExit()
    {
        var sm = I.Get<SubscriptionManager>();
        sm.Unsubscribe(EntityStrategy.CrashEventPath(_entity), _onCrashEvent);
    }

    
    public override void OnEnter()
    {
        var sm = I.Get<SubscriptionManager>();
        sm.Subscribe(EntityStrategy.CrashEventPath(_entity), _onCrashEvent);
    }
}