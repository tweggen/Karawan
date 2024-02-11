using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
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


public class Widget : IDisposable
{   
    protected object _lo = new();
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
                }
                _properties[key] = value;

                impl = _impl;
            }

            if (null != impl)
            {
                impl.OnPropertyChanged(key, oldValue, newValue);
            }
        }
    }


    static public T GetTypedAttribute<T>(object value)
    {
        Type typeAttr = typeof(T);
        try
        {
            return (T)value;
        }
        catch (Exception e)
        {
            ErrorThrow<ArgumentException>(
                $"Unable to cast attribute value for attribute to {typeof(T).Name}");
            return default;
        }
    }
    
    
    public T GetAttr<T>(string name, T defaultValue)
    {
        lock (_lo)
        {
            if (null == _properties)
            {
                return defaultValue;
            }

            if (_properties.TryGetValue(name, out var objValue))
            {
                return GetTypedAttribute<T>(objValue);
            }
            else
            {
                return defaultValue;
            }
        }
    }


    private ALayout _layout;

    public ALayout Layout
    {
        get
        {
            lock (_lo)
            {
                return _layout;
            }
        }

        set
        {
            ALayout oldLayout;
            lock (_lo)
            {
                if (_layout == value) return;
                oldLayout = _layout;
            }

            value.Parent = this;
        }
    }


    public FocusStates FocusState { get; set; } = FocusStates.Unfocussable;
    public SelectionStates SelectionState { get; set; } = SelectionStates.Unselectable;
    
    
    public bool IsFocussed = false;
    public bool IsSelected = false;
    
    
    protected bool _isVisible = true;
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
             * Collect current data, short circuit if everything is
             * done.
             */
            IReadOnlyList<Widget>? children;
            RootWidget root;
            lock (_lo)
            {
                if (_isVisible == value)
                {
                    /*
                     * Short circuit return if there is no change.
                     */
                    return;
                }

                children = _immutableChildrenNL();
                root = _root;
            }

            if (value)
            {
                /*
                 * We shall be visible. This means, we need to realize
                 * everybody of my children who is visible.
                 */
                lock (_lo)
                {
                    _isVisible = true;
                }

                if (null != root)
                {
                    Realize(root);
                }
            }
            else
            {
                /*
                 * We shall become invisible. 
                 */
                // TXWTODO: How we manage to effectively make all children invisible as well?
                lock (_lo)
                {
                    _isVisible = false;
                }

            }
        }
    }

    protected RealizationStates RealizationState = RealizationStates.Unrealized;

    private engine.geom.Rect2 _rectangle;
    
    
    private RootWidget? _root = null;

    public RootWidget? Root
    {
        get
        {
            lock (_lo)
            {
                return _root;
            }
        }
        set
        {
            Widget? oldRoot = null;
            bool isVisible = false;
            IReadOnlyList<Widget> children = null;
            RootWidget root = value;
            
            /*
             * Adding this widget to the root widget enforces realization of the
             * widget as soon it is visible.
             */
            lock (_lo)
            {
                if (_root == value)
                {
                    return;
                }
                
                oldRoot = _root;
                _root = value;
                children = _immutableChildrenNL();
                isVisible = _isVisible;
            }
            
            /*
             * Realize myself, if I am visible, so that children can
             * realize themselves.
             */
            if (isVisible)
            {
                RealizeSelf(root);
            }

            /*
             * Propagate the root to its children.
             */
            if (children != null)
            {
                foreach (var child in children)
                {
                    child.Root = value;
                }
            }
        }
    }

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
    

    protected void RealizeSelf(RootWidget root)
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
                impl = root.Factory.Realize(this);
                if (null == impl)
                {
                    /*
                     * Null is a perfectly valid result, in case no native resources are
                     * required for this widget.
                     */
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


    protected void UnrealizeSelf()
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


    public void UnrealizeChildren()
    {
        IReadOnlyList<Widget> children;
        lock (_lo)
        {
            children = _immutableChildrenNL();
        }
        if (null != children)
        {
            /*
             * We shall become visible
             */
            foreach (var child in children)
            {
                child.Unrealize();
            }
        }
    }
    

    public void Unrealize()
    {
        UnrealizeSelf();
        UnrealizeChildren();
    }


    public void RealizeChildren(RootWidget root)
    {
        IReadOnlyList<Widget> children;
        lock (_lo)
        {
            children = _immutableChildrenNL();
        }
        if (null != children)
        {
            /*
             * We shall become visible. So realize all children who are visible.
             */
            foreach (var child in children)
            {
                if (child.IsVisible)
                {
                    child.Realize(root);
                }
            }
        }
    }


    /**
     * Realize this widget, including its visible children.
     */
    public void Realize(RootWidget root)
    {
        if (null == root)
        {
            ErrorThrow<ArgumentException>("Called with null root widget.");
        }

        RealizeSelf(root);
        RealizeChildren(root);
    }


    public void Dispose()
    {
        Unrealize();
    }
}