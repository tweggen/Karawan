
namespace engine.hierarchy.components
{
    struct OldParent
    {
        public DefaultEcs.Entity Entity;

        public OldParent( in DefaultEcs.Entity entity )
        {
            Entity = entity;
        }

        public OldParent( in Parent parent )
        {
            Entity = parent.Entity;
        }
    }
}
