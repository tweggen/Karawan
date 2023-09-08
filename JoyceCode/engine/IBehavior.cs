
namespace engine
{
    public interface IBehavior
    {
        public void OnCollision(engine.physics.ContactEvent cev);
        /**
         * Called after a given period of inactivity: Sync with reality before
         * continueing your behavior.
         */
        public void Sync(in DefaultEcs.Entity entity);
        
        /**
         * Called per logical frame: Do your behavior.
         */
        public void Behave(in DefaultEcs.Entity entity, float dt);
    }
}
