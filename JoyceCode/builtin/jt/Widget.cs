using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using static engine.Logger;

namespace builtin.jt;


public interface IWidgetImplementation
{
    public void OnPropertyChanged(string key, object oldValue, object newValue);
    public void Unrealize();
}


public enum FocusStates
{
    Unfocussable,
    Focussable
}


public enum SelectionStates
{
    Unselectable,
    Selectable
}


public enum RealizationStates
{
    Unrealized,
    Realizing,
    Realized,
    Unrealizing
}


public interface IFocussable
{
}


public interface Layout
{
}


public class Widget : IDisposable
{
    public required Factory Factory;
    
    private object _lo = new();
    public required string Type;
    private SortedDictionary<string, object>? _properties = null;

    public object this[string key]
    {
        get
        {
            lock (_lo)
            {
                if (null != _properties)
                {
                    if (_properties.TryGetValue(key, out object value))
                    {
                        return value;
                    }
                }

                ErrorThrow<KeyNotFoundException>($"Unable to find key {key} in widget.");
                return null;
            }
        }
        set
        {
            object oldValue = null, newValue = value;
            IWidgetImplementation impl;
            lock (_lo)
            {
                if (null == _properties)
                {
                    _properties = new();
                }
                else
                {
                    _properties.TryGetValue(key, out oldValue);
                    _properties[key] = value;
                }

                impl = _impl;
            }

            if (null != impl)
            {
                impl.OnPropertyChanged(key, oldValue, newValue);
            }
        }
    }

    public FocusStates FocusState { get; set; } = FocusStates.Unfocussable;
    public SelectionStates SelectionState { get; set; } = SelectionStates.Unselectable;

    
    public bool IsFocussed;
    public bool IsSelected;
    
    
    protected bool _isVisible;
    public bool IsVisible
    {
        get
        {
            lock (_lo)
            {
                return _isVisible;
            }
        }

        set
        {
            /*
             * What we are right now.
             */
            bool isVisible;
            IReadOnlyList<Widget>? children;
            lock (_lo)
            {
                if (_isVisible == value)
                {
                    /*
                     * Short circuit return if there is no change.
                     */
                    return;
                }

                isVisible = _isVisible;
                children = _immutableChildrenNL();
            }

            if (value)
            {
                /*
                 * We shall become visible
                 */
                foreach (var child in children)
                {
                    child.Realize();
                }

                lock (_lo)
                {
                    _isVisible = true;
                }

                foreach (var child in children)
                {
                    child.IsVisible = true;
                }
            }
            else
            {
                foreach (var child in children)
                {
                    child.IsVisible = false;
                }

                lock (_lo)
                {
                    _isVisible = false;
                }

            }
        }
    }

    protected RealizationStates RealizationState = RealizationStates.Unrealized;

    private engine.geom.Rect2 _rectangle;
    private Widget? _parent;
    
    private List<Widget>? _children;

    private IReadOnlyList<Widget>? _immutableChildren;
    private IReadOnlyList<Widget>? _immutableChildrenNL()
    {
        if (null == _children)
        {
            return null;
        }

        if (null == _immutableChildren)
        {
            _immutableChildren = _children.ToImmutableList();
        }

        return _immutableChildren;
    }
    
    private Layout? _layout;
    
    
    /**
     * The actual implementation creating entities or resources of some sort.
     */
    private IWidgetImplementation? _impl;
    
    
    public void AddChild(Widget child)
    {
        lock (_lo)
        {
            if (null == _children) _children = new();
            _children.Add(child);
            _immutableChildren = null;
        }
    }

    
    public void RemoveChild(Widget child)
    {
        lock (_lo)
        {
            if (null == _children) return;
            _children.Remove(child);
            _immutableChildren = null;
        }
    }


    public Widget NextWidget()
    {
        lock (_lo)
        {
            if (null == _parent)
            {
                return null;
            }

            if (null == _parent._children)
            {
                ErrorThrow<InvalidOperationException>($"Invalid internal structure: My parent has no children list.");
                return null;
            }

            int idx = _parent._children.IndexOf(this);
            if (-1 == idx)
            {
                ErrorThrow<InvalidOperationException>($"Invalid internal structure: I am not in my parnet's list.");
                return null;
            }

            int l = _parent._children.Count;
            return _parent._children[(idx + 1) % l];
        }
    }


    public Widget PreviousWidget()
    {
        lock (_lo)
        {
            if (null == _parent)
            {
                return null;
            }

            if (null == _parent._children)
            {
                ErrorThrow<InvalidOperationException>($"Invalid internal structure: My parent has no children list.");
                return null;
            }

            int idx = _parent._children.IndexOf(this);
            if (-1 == idx)
            {
                ErrorThrow<InvalidOperationException>($"Invalid internal structure: I am not in my parnet's list.");
                return null;
            }

            int l = _parent._children.Count;
            return _parent._children[(idx + l - 1) % l];
        }
    }
    

    protected void Realize()
    {
        RealizationStates realizationState;
        lock (_lo)
        {
            realizationState = RealizationState;
        }

        if (realizationState == RealizationStates.Unrealized)
        {
            IWidgetImplementation impl = null;

            lock (_lo)
            {
                RealizationState = RealizationStates.Realizing;
            }
            
            try
            {
                impl = Factory.Realize(this);
                if (null == impl)
                {
                    ErrorThrow<InvalidOperationException>($"Creating an implementation returned null.");
                }
            }
            catch (Exception e)
            {
                ErrorThrow<InvalidOperationException>($"Unable to create implementation: {e}.");
                return;
            }
            lock (_lo)
            {
                _impl = impl;
                RealizationState = RealizationStates.Realized;
            }
        }
        else
        {
            if (realizationState != RealizationStates.Realized)
            {
                ErrorThrow<InvalidOperationException>("Called in wrong state.");
            }
        }
    }


    protected void Unrealize()
    {
        RealizationStates realizationState;
        IWidgetImplementation impl;
        lock (_lo)
        {
            realizationState = RealizationState;
            impl = _impl;
        }

        if (realizationState == RealizationStates.Realized)
        {
            lock (_lo)
            {
                RealizationState = RealizationStates.Unrealizing;
            }

            if (impl != null)
            {
                try
                {
                    impl.Unrealize();
                }
                catch (Exception e)
                {
                    ErrorThrow<InvalidOperationException>($"Unable to unrealize implementation: {e}.");
                    return;
                }
            }

            lock (_lo)
            {
                _impl = null;
                RealizationState = RealizationStates.Unrealized;
            }
        }
        else
        {
            if (realizationState != RealizationStates.Unrealized)
            {
                ErrorThrow<InvalidOperationException>("Called in wrong state.");
            }
        }
    }


    public void Dispose()
    {
        Unrealize();
    }
}