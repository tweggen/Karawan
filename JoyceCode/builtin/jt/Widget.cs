using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using engine.gongzuo;
using engine.news;
using ObjLoader.Loader.Common;
using static engine.Logger;

namespace builtin.jt;


public interface IWidgetImplementation : IDisposable
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


public class Widget : IDisposable
{   
    protected object _lo = new();
    public required string Type;
    
    /**
     * Stores the user accessible property data.
     */
    private SortedDictionary<string, object>? _properties = null;
    
    /**
     * Contains anything we compile the properties to after user acesses it.
     */
    private SortedDictionary<string, IDisposable>? _compiledProperties = null;


    private void _invalidateCompiled(string key)
    {
        IDisposable? oCompiled = null;
        lock (_lo)
        {
            if (null == _compiledProperties) return;
            if (_compiledProperties.TryGetValue(key, out oCompiled))
            {
                _compiledProperties.Remove(key);
            }
        }

        if (oCompiled != null)
        {
            oCompiled.Dispose();
        }
    }


    private void _storeCompiled(string key, IDisposable oCompiled)
    {
        IDisposable? oOldCompiled = null;
        lock (_lo)
        {
            if (null == _compiledProperties)
            {
                _compiledProperties = new();
            }
            if (_compiledProperties.TryGetValue(key, out oOldCompiled))
            {
                if (oCompiled == oOldCompiled) return;
                _compiledProperties.Remove(key);
            }
            _compiledProperties[key] = oCompiled;
        }

        if (oOldCompiled != null)
        {
            oOldCompiled.Dispose();
        }
    }
    
    
    private void _storeCompiled_nl(string key, IDisposable oCompiled)
    {
        if (null == _compiledProperties)
        {
            _compiledProperties = new();
        }
        _compiledProperties[key] = oCompiled;
    }
    
    
    public void _invalidateCompiled_nl(string key, out IDisposable? oldCompiled)
    {
        if (null == _compiledProperties)
        {
            oldCompiled = null;
            return;
        }
        if (_compiledProperties.TryGetValue(key, out oldCompiled))
        {
            _compiledProperties.Remove(key);
        }
    }
    
    
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
            IDisposable? oldCompiled = null;

            lock (_lo)
            {
                if (null == _properties)
                {
                    _properties = new();
                }
                else
                {
                    _properties.TryGetValue(key, out oldValue);
                    if (oldValue != newValue)
                    {
                        _invalidateCompiled_nl(key, out oldCompiled);
                    }
                }
                _properties[key] = value;

                impl = _impl;
            }

            if (null != oldCompiled)
            {
                oldCompiled.Dispose();
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


    public bool HasAttr(string name)
    {
        lock (_lo)
        {
            if (null == _properties)
            {
                return false;
            }

            if (_properties.ContainsKey(name))
            {
                return true;
            }
            else
            {
                return false;
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
                _layout = value;
            }

            value.Parent = this;
        }
    }


    public FocusStates FocusState { get; set; } = FocusStates.Focussable;
    public SelectionStates SelectionState { get; set; } = SelectionStates.Unselectable;

    private Widget _wFocussedChild = null;
    /**
     * The child widget of us thatz currently focussed.
     * By default, the first focussable child becomes focussed.
     */
    public Widget? FocussedChild {
        get
        {
            lock (_lo)
            {
                return _wFocussedChild;
            }
        }
        set
        {
            lock (_lo)
            {
                _wFocussedChild = value;
            }
        } 
    }


    public event EventHandler<bool> OnFocusChanged;
    public event EventHandler<Widget> OnFocussedChildChanged;

    
    private bool _isFocussed = false;
    public bool IsFocussed
    {
        get
        {
            lock (_lo)
            {
                return _isFocussed;
            }
        }
        set
        {
            lock (_lo)
            {
                if (_isFocussed == value) return;
                _isFocussed = value;
            }

            this["focussed"] = value;
            OnFocusChanged?.Invoke(this, value);
        }
    }
    
    
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
            
            this["visible"] = value;
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
            ALayout layout = null;
            
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
                layout = _layout;
            }

            if (null != value)
            {
                /*
                 * Realize myself, if I am visible, so that children can
                 * realize themselves.
                 */
                if (isVisible)
                {

                    /*
                     * Finally, layout myself.
                     */
                    if (layout != null)
                    {
                        layout.Activate();
                    }

                    RealizeSelf(root);
                }
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

            /*
             * Finally, if we do not have a root anymore, unrealize.
             */
            if (null == value)
            {
                UnrealizeSelf();
            }
        }
    }
    

    private Widget? _parent = null;
    public Widget? Parent
    {
        get
        {
            lock (_lo)
            {
                return _parent;
            }
        }

        set
        {
            Widget wOldParent;
            lock (_lo)
            {
                if (value == _parent) return;
                wOldParent = _parent;
                _parent = value;
            }

            if (null != wOldParent)
            {
                wOldParent.RemoveChild(this);
            }
        }
    }
    
    
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

    
    public IReadOnlyList<Widget>? Children
    {
        get
        {
            lock (_lo)
            {
                return _immutableChildrenNL();
            }
        }
    }

    public bool HasChildren
    {
        get
        {
            lock (_lo)
            {
                return _children != null && _children.Count > 0;
            }
        }
    }


    protected void _setNewFocus(Widget wNewFocus)
    {
        Widget wOldFocus;
        lock (_lo)
        {
            if (wNewFocus == _wFocussedChild) return;
            
            wOldFocus = _wFocussedChild;
            _wFocussedChild = wNewFocus;
        }

        if (wOldFocus != null)
        {
            wOldFocus.IsFocussed = false;
        }

        if (wNewFocus != null)
        {
            wNewFocus.IsFocussed = true;
        }
        
        OnFocussedChildChanged?.Invoke(this, wNewFocus);
    }
    

    /**
     * The actual implementation creating entities or resources of some sort.
     */
    private IWidgetImplementation? _impl;
    
    
    public virtual void AddChild(Widget child)
    {
        bool doFocusChild = false;
        
        lock (_lo)
        {
            if (null == _children) _children = new();

            _children.Add(child);

            /*
             * If we didn't have a focussed child yet, try to find some.
             */
            if (null == _wFocussedChild)
            {
                if (child.FocusState == FocusStates.Focussable)
                {
                    doFocusChild = true;
                }
            }
            _immutableChildren = null;
        }

        child.Parent = this;

        if (doFocusChild)
        {
            _setNewFocus(child);
        }
    }

    
    public virtual void RemoveChild(Widget child)
    {
        child.Parent = null;
        Widget wNewFocus = null; 
        
        lock (_lo)
        {
            if (null == _children) return;
            
            _children.Remove(child);
            _immutableChildren = null;

            if (_wFocussedChild == child)
            {
                // TXWTODO: There are better ways to find a good follow-up focus than just taking the first child.
                _wFocussedChild = _firstFocussableOwnChildNL();
                wNewFocus = _wFocussedChild;
            }
        }

        _setNewFocus(wNewFocus);
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
        UnrealizeChildren();
        UnrealizeSelf();
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
             * We_ shall become visible. So realize all children who are visible.
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


    protected Widget? _firstFocussableOwnChildNL()
    {
        Widget? wFirst = null;
        if (null != _children)
        {
            wFirst = _children.Find(w => w.FocusState == FocusStates.Focussable);
        }
        
        return wFirst;
    }


    private void _focusOffsetChild(int increment)
    {
        Widget wNewFocus = null;
        lock (_lo)
        {
            if (null == _children)
            {
                return;
            }
            int l = _children.Count;
            if (l == 0)
            {
                return;
            }

            int startIndex;
            if (null == _wFocussedChild)
            {
                startIndex = 0;
            }
            else
            {
                startIndex = _children.IndexOf(_wFocussedChild);
            }

            for (int i = 1; i < l; ++i)
            {
                startIndex += increment;
                var wCand =_children[(startIndex + l) % l];
                if (wCand.FocusState == FocusStates.Focussable)
                {
                    wNewFocus = wCand;
                    break;
                }
            }
        }

        if (null != wNewFocus)
        {
            _setNewFocus(wNewFocus);
        }
    }


    /**
     * Try to focus the previous widget
     */
    public void FocusPreviousChild()
    {
        _focusOffsetChild(-1);
    }
    

    public void FocusNextChild()
    {
        _focusOffsetChild(1);
    }


    protected void _queueLuaScript(string evType, LuaScriptEntry lse, string script, engine.news.Event ev)
    {
        /*
         * First, compile the script (this compiles only if required, checks for a change, again).
         */
        lse.LuaScript = script;
        
        /*
         * Then, execute.
         */
        lse.Call();
    }


    protected LuaScriptEntry _findLuaScriptEntry(string evType, string script)
    {
        LuaScriptEntry? lse = null;
        lock (_lo)
        {
            if (null != _compiledProperties && _compiledProperties.TryGetValue(evType, out var oCompiled))
            {
                lse = oCompiled as LuaScriptEntry;
            }
            else
            {
                lse = new LuaScriptEntry();
                lse.Bind("jt", new LuaBindings());
                _storeCompiled_nl(evType, lse);
            }
        }

        return lse;
    }
    

    protected void _emitEvent(string evType, engine.news.Event ev)
    {
        string script = GetAttr(evType, "");
        if (script.IsNullOrEmpty()) return;

        LuaScriptEntry? lse;
        try
        {
            lse = _findLuaScriptEntry(evType, script);
        }
        catch (Exception e)
        {
            Trace($"Exception while compiling lua handler: {e}");
            return;
        }

        /*
         * Finally execute the script.
         */
        _queueLuaScript(evType, lse, script, ev);
    }


    protected void _emitSelected(engine.news.Event ev)
    {
        _emitEvent("onClick", ev);
    }
    

    /**
     * Default implementation to handle keyboard events.
     * Default behaviour:
     * - If I do not have children
     *   - space or e selects the current item.
     * - If I do have children
     *   - cursor / navigation focusses the next/previous of my children.
     */
    protected virtual void _handleSelfInputEvent(engine.news.Event ev)
    {
        bool haveChildren = HasChildren;
        bool isHorizontal = GetAttr("direction", "vertical") == "horizontal";
        
        switch (ev.Type)
        {
            case engine.news.Event.INPUT_KEY_PRESSED:
                switch (ev.Code)
                {
                    case "(cursorup)":
                    case "W":
                        if (haveChildren && !isHorizontal)
                        {
                            FocusPreviousChild();
                            ev.IsHandled = true;
                        }
                        break;
                    case "(cursordown)":
                    case "S":
                        if (haveChildren && !isHorizontal)
                        {
                            FocusNextChild();
                            ev.IsHandled = true;
                        }
                        break;
                    case "(cursorleft)":
                    case "A":
                        if (haveChildren && !isHorizontal)
                        {
                            FocusPreviousChild();
                            ev.IsHandled = true;
                        }
                        break;
                    case "(cursorright)":
                    case "D":
                        if (haveChildren && !isHorizontal)
                        {
                            FocusNextChild();
                            ev.IsHandled = true;
                        }
                        break;
                    case " ":
                    case "E":
                    case "(enter)":
                        if (!haveChildren)
                        {
                            _emitSelected(ev);
                            ev.IsHandled = true;
                        }
                        break;
                    default:
                        break;
                }

                break;
            default:
                break;
        }
    }
    
    
    /**
     * Try to handle the given event. If you are not able to handle it,
     * pass it up again.
     */
    public void HandleInputEvent(engine.news.Event ev)
    {
        if (ev.IsHandled == true)
        {
            return;
        }
        _handleSelfInputEvent(ev);
        if (ev.IsHandled == true)
        {
            return;
        }

        Parent?.HandleInputEvent(ev);
    }

    
    /**
     * Propoagate the input event to the currently focussed widget.
     * The leaf widget that is focussed will call the handle chain bottom-up.
     */
    public void PropagateInputEvent(engine.news.Event ev)
    {
        Widget wFocussedChild;
        lock (_lo)
        {
            wFocussedChild = _wFocussedChild;
        }

        if (null == wFocussedChild)
        {
            HandleInputEvent(ev);
        }
        else
        {
            wFocussedChild.PropagateInputEvent(ev);
        }
    }
    

    public void Dispose()
    {
        Unrealize();
        // TXWTODO: We know that Unrealize basically is a dispose. Technically, we
        // would love to have a second dispose iteration.
    }
}