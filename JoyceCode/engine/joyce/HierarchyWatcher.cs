using DefaultEcs;
using engine.joyce.components;

namespace engine.joyce;


/*
 * We need to take to remove 
 */
public class HierarchyWatcher : AComponentWatcher<engine.joyce.components.Parent>
{
    protected override void _onComponentRemoved(in Entity entity, in Parent cOldStrategy)
    {
        /*
         * If a parent component is being removed, we shall remove the child from its
         * parent children component.
         *
         * We should, however, note, that the parent's entity might very well not exist
         * anymore.
         */
        if (cOldStrategy.Entity.IsAlive)
        {
            if (cOldStrategy.Entity.Has<Children>())
            {
                ref var cChildren = ref cOldStrategy.Entity.Get<Children>();
                cChildren.Entities?.Remove(cOldStrategy.Entity);
            }
        }
    }

    protected override void _onComponentChanged(in Entity entity, in Parent cOldStrategy, in Parent cNewBehavior)
    {
        if (cOldStrategy.Entity != cNewBehavior.Entity)
        {
            _onComponentRemoved(entity, cOldStrategy);
        }
    }
}