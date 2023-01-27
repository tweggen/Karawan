
namespace engine.joyce
{
    public class World3
    {
        private engine.Engine _engine;
        private DefaultEcs.Entity _rootEntity;

        public void Remove()
        {
        }

        public void Add()
        {
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
