using engine;
using engine.behave;
using engine.news;
using nogame.characters;

namespace nogame.npcs.niceday;

public class RestStrategy : AEntityStrategyPart
{
    public required CharacterModelDescription CharacterModelDescription { get; init; }
    public required CharacterState CharacterState { get; init; }
    public required PositionDescription PositionDescription { get; init; }

    
    private void _onCrashHitEvent(Event ev)
    {
        /*
         * If we are not already in recover, transition to recover.
         */
    }
    
    
    public override void OnExit()
    {
        var sm = I.Get<SubscriptionManager>();
        sm.Unsubscribe(EntityStrategy.CrashHitEventPath(_entity), _onCrashHitEvent);

        _entity.Remove<engine.behave.components.Behavior>();
    }
    
    
    public override void OnEnter()
    {
        /*
         * The model description is handed over so that the behaviour can drive the idle
         * animation. Without it a resting niceday NPC has no animation driver at all and
         * stands in its bind pose - a T-pose - for its whole life.
         */
        _entity.Set(new engine.behave.components.Behavior(new NearbyBehavior()
        {
            EPOI = _entity,
            PositionDescription = PositionDescription,
            CharacterModelDescription = CharacterModelDescription
        }));

        var sm = I.Get<SubscriptionManager>();
        sm.Subscribe(EntityStrategy.CrashHitEventPath(_entity), _onCrashHitEvent);
    }
}