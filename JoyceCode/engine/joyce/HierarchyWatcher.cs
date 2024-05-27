using DefaultEcs;
using engine.joyce.components;

namespace engine.joyce;


/*
 * We need to take to remove 
 */
public class HierarchyWatcher : AComponentWatcher<engine.joyce.components.Parent>
{
    protected override void _onComponentRemoved(in Entity entity, in Parent cOldComponent)
    {
        /*
         * If a parent component is being removed, we shall remove the child from its
         * parent children component.
         *
         * We should, however, note, that the parent's entity might very well not exist
         * anymore.
         */
        if (cOldComponent.Entity.IsAlive)
        {
            if (cOldComponent.Entity.Has<Children>())
            {
                ref var cChildren = ref cOldComponent.Entity.Get<Children>();
                cChildren.Entities?.Remove(cOldComponent.Entity);
            }
        }
    }

    protected override void _onComponentChanged(in Entity entity, in Parent cOldComponent, in Parent cNewComponent)
    {
        if (cOldComponent.Entity != cNewComponent.Entity)
        {
            _onComponentRemoved(entity, cOldComponent);
        }
    }
}