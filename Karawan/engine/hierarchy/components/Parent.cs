
namespace Karawan.engine.hierarchy.components
{
    struct Parent
    {
        public DefaultEcs.Entity Entity;

        public Parent( in DefaultEcs.Entity entity )
        {
            Entity = entity;
        }
    }
}
