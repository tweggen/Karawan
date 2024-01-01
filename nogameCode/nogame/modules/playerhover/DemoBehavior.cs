#if false

using DefaultEcs;
namespace nogame.modules.playerhover;

public class DemoBehavior : engine.IBehavior
{
    private engine.Engine _engine;
    private DefaultEcs.Entity _eHike;


    public void OnCollision(ContactEvent cev)
    {
        /*
         * We just ignorew contacts in the demo mode.
         */
    }


    /**
     * Initialize our current state from the behavior of the entity.
     */
    public void Sync(in Entity entity)
    {
        
    }
    
    
    public void Behave(in Entity entity, float dt)
    {
    }

    
    public void OnDetach(in Entity entity)
    {
        _engine = null;
    }
    
    
    public void OnAttach(in engine.Engine engine0, in Entity entity)
    {
        _engine = engine0;
        _eHike = entity;
    }
}
#endif