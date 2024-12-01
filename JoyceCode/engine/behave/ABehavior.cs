using System.Text.Json;
using System.Text.Json.Nodes;
using DefaultEcs;
using engine.news;
using engine.physics;

namespace engine.behave;

public class ABehavior : IBehavior
{
    public string DetachEventType = null;
    public string DetachEventCode = null;
    
    public virtual void OnCollision(ContactEvent cev)
    {
    }

    public virtual void Behave(in Entity entity, float dt)
    {
    }

    public virtual void Sync(in Entity entity)
    {
    }

    public virtual void SetupFrom(JsonElement je)
    {
    }

    
    public virtual void SaveTo(ref JsonObject jo)
    {
    }


    public virtual void OnDetach(in Entity entity)
    {
        if (null != DetachEventType)
        {
            I.Get<EventQueue>().Push(
                new news.Event(
                    news.Event.BEHAVIOR_LOST_CUSTOM_EVENT+DetachEventType,
                    DetachEventCode));
        }
    }

    public virtual void OnAttach(in Engine engine0, in Entity entity)
    {
    }

    public virtual void InRange(in Engine engine0, in Entity entity)
    {
    }

    public virtual void OutOfRange(in Engine engine0, in Entity entity)
    {
    }
}