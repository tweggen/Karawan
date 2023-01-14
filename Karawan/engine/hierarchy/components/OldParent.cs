
namespace Karawan.engine.hierarchy.components
{
    struct OldParent
    {
        public DefaultEcs.Entity Entity;

        public OldParent( DefaultEcs.Entity entity )
        {
            Entity = entity;
        }

        public OldParent( Parent parent )
        {
            Entity = parent.Entity;
        }
    }
}
