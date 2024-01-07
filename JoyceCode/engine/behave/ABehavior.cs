using DefaultEcs;
using engine.physics;

namespace engine.behave;

public class ABehavior : IBehavior
{
    public virtual void OnCollision(ContactEvent cev)
    {
    }

    public virtual void Behave(in Entity entity, float dt)
    {
    }

    public virtual void Sync(in Entity entity)
    {
    }

    public virtual void OnDetach(in Entity entity)
    {
    }

    public virtual void OnAttach(in Engine engine0, in Entity entity)
    {
    }
}