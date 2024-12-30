using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DefaultEcs;
using engine.news;
using engine.physics;

namespace engine.behave;

public class ABehavior : IBehavior
{
    protected Engine _engine;
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

    public virtual Func<Task> SetupFrom(JsonElement je)
    {
        return new Func<Task>(async () => { });
    }

    
    public virtual void SaveTo(JsonObject jo)
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
        _engine = engine0;
    }

    public virtual void InRange(in Engine engine0, in Entity entity)
    {
    }

    public virtual void OutOfRange(in Engine engine0, in Entity entity)
    {
    }
}