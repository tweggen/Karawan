
namespace engine.joyce
{

    /**
     * A world object is the root for rendering hierarchial entities
     * into a camera.
     * 
     * Each Camera object is associated with exactly one world object.
     * 
     * Note, that we also are able to render objects beside this hierarchial
     * level.
     */
    public class World3
    {
        private engine.Engine _engine;
        private DefaultEcs.Entity _rootEntity;


        public engine.Engine GetEngine()
        {
            return _engine;
        }


        public DefaultEcs.Entity GetEntity()
        {
            return _rootEntity;
        }


        public void Remove()
        {
            _rootEntity.Disable();
        }


        public void Add()
        {
            _rootEntity.Enable();
        }


        public World3(engine.Engine engine)
        {
            _engine = engine;
            _rootEntity = _engine.GetEcsWorld().CreateEntity();
            _rootEntity.Disable();
            _rootEntity.Set<transform.components.Transform3ToParent>(new transform.components.Transform3ToParent());
        }
    }
}
