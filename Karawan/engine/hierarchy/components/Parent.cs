
namespace Karawan.engine.hierarchy.components
{
    struct Parent
    {
        public DefaultEcs.Entity Entity;

        public Parent( DefaultEcs.Entity entity )
        {
            Entity = entity;
        }
    }
}
