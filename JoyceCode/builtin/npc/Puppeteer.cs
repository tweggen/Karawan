using engine.behave;
using engine.joyce;

namespace builtin.npc;

/**
 * Sets up various aspects of an entity, like the current behavior,
 * the current physics, emitting sounds etc. .
 */
public class Puppeteer
{
    private DefaultEcs.Entity _entity;


    /**
     *
     */
    public void SetKinematic()
    {
        
    }


    public void SetDynamic()
    {
        
    }


    public void SetTerminate()
    {
        
    }


    public void PlaySound()
    {
        
    }


    public void SetNavigator(INavigator navigator)
    {
        
    }


    public void SetAnimationState(AnimationState animState)
    {
        
    }
    

    /**
     * Set up a new behavior implementation, copying the metainformation from
     * the current behavior.
     */
    public void SetBehavior(IBehavior behavior)
    {
        if (_entity.Has<engine.behave.components.Behavior>())
        {
            /*
             * Create a copy and assign the new behavior.
             * By exchanging the entire components, the attach/detach handlers are called.
             */
            var cBehavior = _entity.Get<engine.behave.components.Behavior>().Provider;
            _entity.Set(cBehavior);
        }
    }


    public void SetBehavior(in engine.behave.components.Behavior cBehavior)
    {
        _entity.Set(cBehavior);
    }
    
    
    public Puppeteer(DefaultEcs.Entity entity)
    {
        _entity = entity;
    }
}