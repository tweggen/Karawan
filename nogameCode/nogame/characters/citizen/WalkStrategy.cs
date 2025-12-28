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
    public required CharacterModelDescription CharacterModelDescription { get; init; }

    private WalkBehavior _walkBehavior;
    
    /**
     * We need a walk behavior that exceeds our lifetime.
     */
    public required INavigator Navigator { get; init; }
    

    #region IStrategyPart
    
    /**
     * Remove walk behavior and clean up.
     */
    public override void OnExit()
    {
        _entity.Remove<engine.behave.components.Behavior>();
    }

    
    /**
     * Add walk behavior and initialize.
     */
    public override void OnEnter()
    {
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