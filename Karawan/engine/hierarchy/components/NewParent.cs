
namespace Karawan.engine.hierarchy.components
{
    struct NewParent
    {
        public DefaultEcs.Entity Entity;

        public NewParent( DefaultEcs.Entity newParentEntity )
        {
            Entity = newParentEntity;
        }
    }
}
