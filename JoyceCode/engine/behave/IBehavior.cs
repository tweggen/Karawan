
using System;

namespace engine.behave;

public interface IBehavior
{
    public void OnCollision(engine.physics.ContactEvent cev);

    /**
     * Called per logical frame: Do your behavior.
     */
    public void Behave(in DefaultEcs.Entity entity, float dt);
    
    /**
     * Called after a given period of inactivity: Sync with reality before
     * continueing your behavior.
     */
    public void Sync(in DefaultEcs.Entity entity);

    /**
     * On Detach
     */
    public void OnDetach(in DefaultEcs.Entity entity);

    /**
     * Called after the behavior has been attached to this entity.
     */
    public void OnAttach(in engine.Engine engine0, in DefaultEcs.Entity entity);

    public void InRange(in engine.Engine engine0, in DefaultEcs.Entity entity);
    public void OutOfRange(in engine.Engine engine0, in DefaultEcs.Entity entity);
}
  