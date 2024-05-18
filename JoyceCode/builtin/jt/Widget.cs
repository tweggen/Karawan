using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using engine;
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
    private static object _classLock = new();
    private static uint _nextId = 0;
    public uint Id;

    private string _widgetEventPath;
    
    /**
     * Stores the user accessible property data.
     */
    private SortedDictionary<string, object>? _properties = null;
    
    /**
     * Contains anything we compile the properties to after user acesses it.
     */
    private SortedDictionary<string, IDisposable>? _compiledProperties = null;

    
    public virtual (float Width, float Height) SizeHint()
    {
        ALayout layout;
        lock (_lo)
        {
            layout = _layout;
        }

        if (null != layout)
        {
            return layout.SizeHint();
        }
        else
        {
            return (0f, 0f);
        }
    }


    public virtual float HeightForWidth(float w)
    {
        return 0f;
    }
    

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
    
    
    public virtual object this[string key]
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
            const string strId = "id";
            if (key == strId && Root != null)
            {
                Error($"It is not allowed to change the id string if the widget is belonging to a root.");
                return;
            }
            
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
        protected set
        {
            Widget wParent;
            RootWidget wRoot;
            lock (_lo)
            {
                if (_isFocussed == value) return;
                wRoot = Root;
            }

            if (null != wRoot)
            {
                /*
                 * If we have a root, we totally pass the job to the root.
                 */
                if (!value)
                {
                    wRoot.UnfocusChild(this);
                }
                else
                {
                    wRoot.SetFocussedChild(this);
                }
            }
            else
            {
                /*
                 * We do not have a root, so this is a purely local call, affecting our
                 * local property only.
                 */
                _setFocusLocally(value);
            }
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


    /**
     * Just set the local focussed state, wqthout any context operations.
     */
    protected void _setFocusLocally(bool isFocussed)
    {
        lock (_lo)
        {
            if (_isFocussed == isFocussed)
            {
                return;
            }

            _isFocussed = isFocussed;
        }

        this["focussed"] = isFocussed;
    }
    

    /**
     * This widget was doomed to be unfocussed.
     * Adjust our local flags and inform the parent.
     */
    internal void _unfocusSelf()
    {
        _setFocusLocally(false);
        
        Widget? wParent;
        lock (_lo)
        {
            wParent = Parent;
        }
  
        wParent?._setFocussedChild(null);
    }


    /**
     * This widget was elected to be focussed.
     * Adjust our local flags and inform the parent.
     */
    internal void _focusSelf()
    {
        _setFocusLocally(true);
        
        Widget? wParent;
        lock (_lo)
        {
            wParent = Parent;
        }
        wParent?._setFocussedChild(this);
    }


    protected void _setFocussedChild(Widget wNewFocus)
    {
        Widget wOldFocus;
        lock (_lo)
        {
            if (wNewFocus == _wFocussedChild) return;
            
            wOldFocus = _wFocussedChild;
            _wFocussedChild = wNewFocus;
        }

        OnFocussedChildChanged?.Invoke(this, wNewFocus);
    }
    

    /**
     * The actual implementation creating entities or resources of some sort.
     */
    private IWidgetImplementation? _impl;
    
    
    public virtual void AddChild(Widget child)
    {
        lock (_lo)
        {
            if (null == _children) _children = new();

            _children.Add(child);
            _immutableChildren = null;
        }

        child.Parent = this;
    }

    
    public virtual void RemoveChild(Widget child)
    {
        child.Parent = null;
        Widget? wOldFocus = null;
        
        /*
         * First remove the child from the focus.
         * This also will remove this löcal focus.
         */
        Root?.UnfocusChild(child);
        
        
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


    internal Widget? _findSiblingNL(Widget wStart, int dir)
    {
        if (null == _children)
        {
            return null;
        }
        int l = _children.Count;
        if (l == 0)
        {
            return null;
        }

        int startIndex = _children.IndexOf(wStart);
        if (startIndex < 0)
        {
            ErrorThrow<ArgumentException>("Invalid start widget, not a child of me.");
            return null;
        }

        int siblingIndex = startIndex + dir;
        if (siblingIndex >= 0 && siblingIndex < l)
        {
            return _children[siblingIndex];
        }

        return null;
    }


    internal Widget? _findSibling(Widget wStart, int dir)
    {
        lock (_lo)
        {
            return _findSiblingNL(wStart, dir);
        }
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


    /**
     * Create a symbol binding frame for this widget.
     */
    internal void PushBindings(LuaScriptEntry lse)
    {
        Parser? parser = this["parser"] as Parser;
        if (null != parser)
        {
            parser.PushBindings(lse);
        }
        LuaBindingFrame lbf = new()
        {
            MapBindings = null
            #if false
            new()
            {
                // { "widget", widget API object }
            }
            #endif
        };
        lse.PushBinding(lbf);

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

                /*
                 * Push the bindings for this widget on the binding stack.
                 */
                PushBindings(lse);
                #if false
                lse.Bind("_context", _luaWidgetContext.Value);
                #endif
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
        RootWidget? wRoot = Root;
        if (null == wRoot)
        {
            return;
        }
        
        bool haveChildren = HasChildren;
        bool isHorizontal = GetAttr("direction", "vertical") == "horizontal";

        if (ev.Type.StartsWith(_widgetEventPath))
        {
            string widgetEventType = ev.Type.Substring(_widgetEventPath.Length);
            switch (widgetEventType)
            {
                case "onClick":
                    _emitSelected(ev);
                    ev.IsHandled = true;
                    break;

                default:
                    break;
            }

            return;
        }
        switch (ev.Type)
        {
            case engine.news.Event.INPUT_MOUSE_PRESSED:
            case engine.news.Event.INPUT_TOUCH_PRESSED:
                _emitSelected(ev);
                ev.IsHandled = true;
                break;
            
            case engine.news.Event.INPUT_KEY_PRESSED:
                switch (ev.Code)
                {
                    case "(cursorup)":
                    case "W":
                        if (haveChildren && !isHorizontal)
                        {
                            _setOffsetFocus(-1);
                            ev.IsHandled = true;
                        }
                        break;
                    case "(cursordown)":
                    case "S":
                        if (haveChildren && !isHorizontal)
                        {
                            _setOffsetFocus(1);
                            ev.IsHandled = true;
                        }
                        break;
                    case "(cursorleft)":
                    case "A":
                        if (haveChildren && isHorizontal)
                        {
                            _setOffsetFocus(-1);
                            ev.IsHandled = true;
                        }
                        break;
                    case "(cursorright)":
                    case "D":
                        if (haveChildren && isHorizontal)
                        {
                            _setOffsetFocus(1);
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


    private void _onWidgetEvent(engine.news.Event ev)
    {
        WidgetEvent wev = ev as WidgetEvent;
        Debug.Assert(wev != null);
        Debug.Assert(wev.Widget == this);
        // TXWTODO: EVents not always are targeted.
        HandleInputEvent(ev);
    }
    

    private void _unsubscribeEvents()
    {
        I.Get<SubscriptionManager>().Unsubscribe($"{_widgetEventPath}onClick", _onWidgetEvent);
    }
    
    
    private void _subscribeEvents()
    {
        I.Get<SubscriptionManager>().Subscribe($"{_widgetEventPath}onClick", _onWidgetEvent);
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
    public virtual void PropagateInputEvent(engine.news.Event ev)
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
    

    public Widget? FindFirstFocussableChild()
    {
        return FindOffsetFocussableChild(this, 1);
    }

    
    /**
     * Return a pointer to the given widget if it is my direct
     * or indirect child.
     */
    public Widget? FindMyChild(Widget? wStart)
    {
        Widget? wParent = wStart;
        while (null != wParent)
        {
            if (this == wParent)
            {
                return wStart;
            }

            wParent = wParent.Parent;
        }

        return null;
    }

    
    /**
     * Find the next child that is "condition" after (in direction of dir) the specified
     * child. The child needs to be a direct or indirect child of this.
     *
     * Algorithm: Depth first traversal, starting with wCurrent->next
     * - we already are checked.
     * - if we have children, so call ourselves recursive on our first child.
     * - then move on to our next sibling. Call ourselfes recursive.
     * - if no child was left
     */
    public Widget? FindNextChild(Widget wCurrent, int dir, Func<Widget, bool> condition)
    {
        if (null == wCurrent)
        {
            ErrorThrow<ArgumentNullException>("Passed a null top widget.");
            return null;
        }
        
        Widget? wMatch = null;

        /*
         * First traverse down to our children.
         */
        {
            var currentChildren = wCurrent.Children;
            if (null != currentChildren && currentChildren.Count > 0)
            {
                var wFirstChild = currentChildren[dir>0?0:currentChildren.Count-1];
                if (condition(wFirstChild))
                {
                    return wFirstChild;
                }
                wMatch = FindNextChild(wFirstChild, dir, condition);
                if (wMatch != null)
                {
                    return wMatch;
                }
            }
        }

        /*
         * Now, traverse to our next sibling in the given direction.
         */
        Widget? wCurrentParent = wCurrent.Parent;
        if (null == wCurrentParent)
        {
            /*
             * If we do not have a parent, which we usually should have,
             * we do not continue that direction.
             */
            return null;
        }

        {
            var wSibling = wCurrentParent._findSibling(wCurrent, dir);
            if (wSibling != null)
            {
                if (condition(wSibling))
                {
                    return wSibling;
                }

                wMatch = FindNextChild(wSibling, dir, condition);
                if (wMatch != null)
                {
                    return wMatch;
                }
            }
        }
        
        /*
         * We did not find it below and not within our siblings.
         * So continue from our parent, unless the parent is the start.
         */
        if (wCurrentParent != this)
        {
            while (true)
            {
                Widget? wParentParent = wCurrentParent.Parent;
                
                if (null == wParentParent)
                {
                    /*
                     * If our current parent does not have a parent, it does not have a sibling
                     * and as such we do not have a next.
                     */
                    return null;
                }

                Widget? wParentSibling = wParentParent._findSibling(wCurrentParent, dir);

                if (null != wParentSibling)
                {
                    if (condition(wParentSibling))
                    {
                        return wParentSibling;
                    }
                    wMatch = FindNextChild(wParentSibling, dir, condition);
                    if (wMatch != null)
                    {
                        return wMatch;
                    }
                } 
                else
                {
                    /*
                     * If this parent has no sibling, traverse up to it's parent and continue.
                     */
                    wCurrentParent = wParentParent;
                }
            }
        }

        /*
         * We did not find it at all.
         */
        return null;
    }


    public Widget? FindOffsetFocussableChild(Widget? wStart, int dir)
    {
        return FindNextChild(wStart, dir, w => w.FocusState == FocusStates.Focussable);
    }


    /**
     * Focus the next/prev focussable child, if the current focus is
     * within this parent.
     * If it is not, select the first / last focussable child.
     */
    private Widget? _findOffsetFocus(int dir)
    {
        RootWidget? wRoot = Root;
        if (null == wRoot)
        {
            return null;
        }

        Widget? wOldFocus = wRoot.GetFocussedChild();
        if (null != wOldFocus)
        {
            /*
             * If we had a focussed widget, keep it non-null only,
             * if it also is below me,
             */
            wOldFocus = FindMyChild(wOldFocus);
        }
        
        if (null == wOldFocus)
        {
            /*
             * If we did not have a focus at all, or if the focus
             * was outside my scope, take the first
             * element in the desired direction.
             */
            return FindOffsetFocussableChild(this, dir);
        }
        else
        {
            Widget? wNewFocus = FindOffsetFocussableChild(wOldFocus, dir);
            if (null == wNewFocus)
            {
                return FindOffsetFocussableChild(this, dir);
            }
            else
            {
                return wNewFocus;
            }
        }
    }


    private void _setOffsetFocus(int dir)
    {
        RootWidget? wRoot = Root;
        if (null == wRoot)
        {
            return;
        }

        Widget? wNewFocus = _findOffsetFocus(dir);
        if (wNewFocus != null)
        {
            wRoot.SetFocussedChild(wNewFocus);
        }
    }
    
  
    public void Dispose()
    {
        Unrealize();
        _unsubscribeEvents();
        // TXWTODO: We know that Unrealize basically is a dispose. Technically, we
        // would love to have a second dispose iteration.
    }

    public Widget()
    {
        lock (_classLock)
        {
            Id = _nextId++;
            _widgetEventPath = $"builtin.jt.widget.{Id}.";
        }
        _subscribeEvents();
    }
}