using System.Collections.Generic;
using DefaultEcs;
using engine;
using engine.behave;
using engine.physics;
using engine.physics.actions;

namespace Joyce.engine.behave;

public class StateBehavior : IBehavior
{
    private object _lo = new();
    
    private SortedDictionary<string, IBehavior> _mapBehaviors = new();
    private string? _strCurrentBehavior = null;
    private IBehavior? _currentBehavior = null;
    
    public void OnCollision(ContactEvent cev)
    {
        IBehavior? behavior;
        lock (_lo)
        {
            behavior = _currentBehavior;
        }
        behavior?.OnCollision(cev);
    }

    public void Behave(in Entity entity, float dt)
    {
        IBehavior? behavior;
        lock (_lo)
        {
            behavior = _currentBehavior;
        }
        behavior?.Behave(entity, dt);
    }

    public void Sync(in Entity entity)
    {
        IBehavior? behavior;
        lock (_lo)
        {
            behavior = _currentBehavior;
        }
        behavior?.Sync(entity);
    }

    public void OnDetach(in Entity entity)
    {
        IBehavior? behavior;
        lock (_lo)
        {
            behavior = _currentBehavior;
        }
        behavior?.OnDetach(entity);
    }

    public void OnAttach(in Engine engine0, in Entity entity)
    {
        IBehavior? behavior;
        lock (_lo)
        {
            behavior = _currentBehavior;
        }
        behavior?.OnAttach(engine0, entity);
    }

    public void InRange(in Engine engine0, in Entity entity)
    {
        IBehavior? behavior;
        lock (_lo)
        {
            behavior = _currentBehavior;
        }
        behavior?.InRange(engine0, entity);
    }

    public void OutOfRange(in Engine engine0, in Entity entity)
    {
        IBehavior? behavior;
        lock (_lo)
        {
            behavior = _currentBehavior;
        }
        behavior?.OutOfRange(engine0, entity);
    }
    
    // TXWTODO: How to add behavior
}