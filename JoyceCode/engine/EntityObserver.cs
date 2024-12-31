using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using DefaultEcs;

using static engine.Logger;


namespace engine;

public class EntityObserver
{
    private object _lo = new();
    
    private List<Action<Entity>> _listWithActions = new();
    private List<Action<Entity>> _listOnChange = new();

    private Entity _currentValue = default;

    public Entity Value
    {
        get  
        {
            lock (_lo)
            {
                return _currentValue;
            }
        }

        set
        {
            SetCurrentValue(value);    
        }
    }


    public bool TryGet(out Entity e)
    {
        lock (_lo)
        {
            e = _currentValue;
            return e != default;
        }
    }
    
    
    public void SetCurrentValue(Entity newValue)
    {
        List<Action<Entity>> listWithActions = null;
        ImmutableList<Action<Entity>> listOnChange = null;
        lock (_lo)
        {
            if (_currentValue == newValue)
            {
                return;
            }
            _currentValue = newValue;
            if (_listWithActions.Count > 0)
            {
                listWithActions = _listWithActions;
                _listWithActions = new();
            }

            listOnChange = _listOnChange.ToImmutableList();
        }

        if (null != listWithActions)
        {
            foreach (var a in listWithActions)
            {
                try
                {
                    a(newValue);
                }
                catch (Exception e)
                {
                    Error($"Error notifying with entity action: {e}");
                }
            }
        }

        foreach (var a in listOnChange)
        {
            try
            {
                a(newValue);
            }
            catch (Exception e)
            {
                Error($"Error notifying on change action: {e}");
            }
        }
    }

    public void AddOnChange(Action<Entity> func)
    {
        lock (_lo)
        {
            _listOnChange.Add(func);
        }
    }


    public void RemoveOnChange(Action<Entity> func)
    {
        lock (_lo)
        {
            _listOnChange.Remove(func);
        }
    }


    public void CallWithEntity(Action<Entity> func)
    {
        bool haveValue = false;
        Entity currentValue = default;

        lock (_lo)
        {
            if (_currentValue != default)
            {
                haveValue = true;
                currentValue = _currentValue;
            }
            else
            {
                _listWithActions.Add(func);
            }
        }

        if (haveValue)
        {
            func(currentValue);
        }
    }
}