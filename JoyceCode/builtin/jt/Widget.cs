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
    public LuaBindingFrame? BuildLuaBindingFrame = null;

    private string _widgetEventPath;

    public enum OffsetOrientation
    {
        DontCare,
        Horizontal,
        Vertical
    };
    
    /**
     * Stores the user accessible property data.
     */
    private SortedDictionary<string, object>? _properties = null;

    private Lazy<CompiledCache> _compiledCache = new(() => new CompiledCache());
    
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


    private object _resolvePropertyValue(string strProperty, object oLiteralValue)
    {
        if (oLiteralValue is string)
        {
            string strLiteralValue = (string)oLiteralValue;
            if (!String.IsNullOrEmpty(strLiteralValue) && strLiteralValue[0] == '{')
            {
                int l = strLiteralValue.Length;
                if (l > 1 && strLiteralValue[1] == '{' || l<2)
                {
                    return oLiteralValue;
                }

                // TXWTODO: Cache the lookup.
                string strScript = strLiteralValue.Substring(1, l - 2);

                var lse = _compiledCache.Value.Find(strProperty, strScript, lse => PushContext(lse));
                return lse.CallSingleResult();
            }
            else
            {
                return oLiteralValue;
            }
        }
        else
        {
            return oLiteralValue;
        }
    }


    private void _tryRemoveFocusFor(object oldValue)
    {
        var strOldValue = oldValue as string;
        if (null == strOldValue) return;

        RootWidget? wRoot = Root;
        if (null == wRoot) return;
        
        wRoot.RemoveFocusFor(strOldValue, GetAttr("id", ""));
    }


    private void _tryAddFocusFor(object newValue)
    {
        var strNewValue = newValue as string;
        if (null == strNewValue) return;

        RootWidget? wRoot = Root;
        if (null == wRoot) return;
        
        wRoot.AddFocusFor(strNewValue, GetAttr("id", ""));
    }


    protected virtual void _onPropertyChanged(string key, object oldValue, object newValue)
    {
        
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
                        return _resolvePropertyValue(key, value);
                    }
                }
                
                const string strId = "id";
                if (strId == key)
                {
                    return $"{Id}";
                }

                ErrorThrow<KeyNotFoundException>($"Unable to find key {key} in widget.");
                return null;
            }
        }
        set
        {
            const string strId = "id";
            const string strFocusFor = "focusFor";
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
                        _compiledCache.Value.Invalidate(key, out oldCompiled);
                    }
                    else
                    {
                        // TXWTODO: It must work that way as well!!
                        // return;
                    }
                }
                _properties[key] = value;

                impl = _impl;
            }

            if (null != oldCompiled)
            {
                oldCompiled.Dispose();
            }

            _onPropertyChanged(key, oldValue, newValue);
            
            if (null != impl)
            {
                impl.OnPropertyChanged(key, oldValue, newValue);
            }

            if (key == strFocusFor)
            {
                if (oldValue != newValue)
                {
                    _tryRemoveFocusFor(oldValue);
                    _tryAddFocusFor(newValue);
                }
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
    
    
    public T GetAttr<T>(string key, T defaultValue)
    {
        lock (_lo)
        {
            if (null != _properties)
            {
                if (_properties.TryGetValue(key, out object value))
                {
                    return GetTypedAttribute<T>(_resolvePropertyValue(key, value));
                }
            }
                
            const string strId = "id";
            if (strId == key)
            {
                return GetTypedAttribute<T>($"{Id}");
            }

            return defaultValue;
        }
    }


    public OffsetOrientation GetSelector()
    {
        switch (GetAttr("selector", ""))
        {
            case "horizontal": return OffsetOrientation.Horizontal;
            case "vertical": return OffsetOrientation.Vertical;
            default:
                return OffsetOrientation.DontCare;
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
  

    public bool IsVisuallyFocussed
    {
        get
        {
            if (IsFocussed)
            {
                return true;
            }
            string idFocusFor = GetAttr("focusFor", "");
            if (string.IsNullOrEmpty(idFocusFor))
            {
                return false;
            }

            var wRoot = Root;
            if (null == wRoot)
            {
                return false;
            }

            wRoot.GetChild(idFocusFor, out var wFocusFor);
            if (null == wFocusFor)
            {
                return false;
            }

            return wFocusFor.IsVisuallyFocussed;
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
            ImmutableList<Widget>? children;
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
            ImmutableList<Widget> children = null;
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
                value.RegisterChild(this);
                {
                    string strFocusFor = GetAttr("focusFor", "");
                    if (!strFocusFor.IsNullOrEmpty())
                    {
                        value.AddFocusFor(strFocusFor, GetAttr("id", ""));
                    }
                }
                
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
                RootWidget? wRoot = (oldRoot as RootWidget);
                wRoot?.UnfocusChild(this);
                UnrealizeSelf();
                {
                    string strFocusFor = GetAttr("focusFor", "");
                    if (!strFocusFor.IsNullOrEmpty())
                    {
                        wRoot?.RemoveFocusFor(strFocusFor, GetAttr("id", ""));
                    }
                }
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

    private ImmutableList<Widget>? _immutableChildren;
    private ImmutableList<Widget>? _immutableChildrenNL()
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

    
    public ImmutableList<Widget>? Children
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
    
    
    public virtual void AddChild(Widget wChild)
    {
        lock (_lo)
        {
            if (null == _children) _children = new();

            _children.Add(wChild);
            _immutableChildren = null;
        }

        wChild.Parent = this;
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
            IWidgetImplementation impl;

            lock (_lo)
            {
                RealizationState = RealizationStates.Realizing;
            }
            
            try
            {
                impl = root.ImplementationFactory.Realize(this);
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
        ImmutableList<Widget> children;
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
        ImmutableList<Widget> children;
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


    internal Widget? _findSiblingNL(Widget wStart, OffsetOrientation ori, int dir)
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

        if (ori == OffsetOrientation.DontCare
            || GetSelector() == OffsetOrientation.DontCare
            || GetSelector() == ori)
        {
            int siblingIndex = startIndex + dir;
            if (siblingIndex >= 0 && siblingIndex < l)
            {
                return _children[siblingIndex];
            }
        }

        return null;
    }


    internal Widget? _findSibling(Widget wStart, OffsetOrientation ori, int dir)
    {
        lock (_lo)
        {
            return _findSiblingNL(wStart, ori, dir);
        }
    }
    

    internal void PushBindings(LuaScriptEntry lse)
    {
        if (BuildLuaBindingFrame != null)
        {
            lse.PushBinding(BuildLuaBindingFrame);
        }

        LuaBindingFrame lbf = new()
        {
            MapBindings = new SortedDictionary<string, object>() { { "widget", new LuaWidgetContext(this) } } 
        };

        lse.PushBinding(lbf);
    }
    

    internal void PushContext(LuaScriptEntry lse)
    {
        /*
         * We need the bindings from the engine...
         */
        I.Get<engine.gongzuo.API>().PushBindings(lse);
        
        /*
         * ...the bindings as defined by the parser for the jt context...
         */
        Parser? parser = this["parser"] as Parser;
        if (null != parser)
        {
            parser.PushBindings(lse);
        }

        /*
         * And finally, the bindings specifically for this widget.
         */
        PushBindings(lse);
    }


    protected LuaScriptEntry _findLuaScriptEntry(string evType, string script)
        => _compiledCache.Value.Find(evType, script, lse => PushContext(lse));
    

    protected object _evaluateProperty(string propName)
    {
        string script = GetAttr(propName, "");
        if (script.IsNullOrEmpty()) return default;

        LuaScriptEntry? lse;
        try
        {
            lse = _findLuaScriptEntry(propName, script);
        }
        catch (Exception e)
        {
            Trace($"Exception while compiling lua handler: {e}");
            return default;
        }

        /*
         * Finally execute the script.
         */
        return lse.CallStringResult();
        
    }
    

    protected void _emitEvent(string evType)
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
        lse.Call();
    }


    protected void _emitSelected(engine.news.Event ev)
    {
        _emitEvent("onClick");
    }


    private void _onClick(engine.news.Event ev)
    {
        /*
         * Focus ourselves, if we are focussable.
         */
        if (FocusState == FocusStates.Focussable)
        {
            Root?.SetFocussedChild(this);
        }

        _emitSelected(ev);
        ev.IsHandled = true;
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
        var selector = GetSelector();

        if (ev.Type.StartsWith(_widgetEventPath))
        {
            string widgetEventType = ev.Type.Substring(_widgetEventPath.Length);
            switch (widgetEventType)
            {
                case "onClick":
                    _onClick(ev);
                    break;
            }

            return;
        }

        var actionUp = (Event ev) =>
        {
            if (haveChildren && !isHorizontal)
            {
                _setOffsetFocus(OffsetOrientation.Vertical, -1);
                ev.IsHandled = true;
            }
        };
        var actionDown = (Event ev) =>
        {
            if (haveChildren && !isHorizontal)
            {
                _setOffsetFocus(OffsetOrientation.Vertical, 1);
                ev.IsHandled = true;
            }
        };
        var actionLeft = (Event ev) =>
        {
            if (haveChildren && isHorizontal)
            {
                _setOffsetFocus(OffsetOrientation.Horizontal, -1);
                ev.IsHandled = true;
            }
        };
        var actionRight = (Event ev) =>
        {
            if (haveChildren && isHorizontal)
            {
                _setOffsetFocus(OffsetOrientation.Horizontal, 1);
                ev.IsHandled = true;
            }
        };
        var actionSelect = (Event ev) =>
        {
            if (!haveChildren)
            {
                _emitSelected(ev);
                ev.IsHandled = true;
            }
        };
        
        
        switch (ev.Type)
        {
            case engine.news.Event.INPUT_MOUSE_PRESSED:
            case engine.news.Event.INPUT_TOUCH_PRESSED:
                _onClick(ev);
                break;
            
            case engine.news.Event.INPUT_BUTTON_PRESSED:
                switch (ev.Code)
                {
                    case "<cursorup>":
                        actionUp(ev);
                        break;
                    case "<cursordown>":
                        actionDown(ev);
                        break;
                    case "<cursorleft>":
                        actionLeft(ev);
                        break;
                    case "<cursorright>":
                        actionRight(ev);
                        break;
                    case "<interact>":
                        actionSelect(ev);
                        break;
                }
                break;
            
            case engine.news.Event.INPUT_KEY_PRESSED:
                switch (ev.Code)
                {
                    case "(cursorup)":
                    case "w":
                        actionUp(ev);
                        break;
                    case "(cursordown)":
                    case "s":
                        actionDown(ev);
                        break;
                    case "(cursorleft)":
                    case "a":
                        actionLeft(ev);
                        break;
                    case "(cursorright)":
                    case "d":
                        actionRight(ev);
                        break;
                    case " ":
                    case "e":
                    case "(enter)":
                        actionSelect(ev);
                        break;
                    case "(tab)":
                        if (IsFocussed)
                        {
                            wRoot = Root;
                            if (wRoot != null)
                            {
                                var wNewFocus = wRoot.FindOffsetFocussableChild(this, OffsetOrientation.DontCare, 1);
                                if (wNewFocus != null)
                                {
                                    wRoot.SetFocussedChild(wNewFocus);
                                    ev.IsHandled = true;
                                }
                            }
                        }
                        break;
                }

                break;
        }
    }


    private void _onWidgetEvent(engine.news.Event ev)
    {
        WidgetEvent wev = ev as WidgetEvent;
        Debug.Assert(wev != null);
        Debug.Assert(wev.Widget == this);
        // TXWTODO: Events not always are targeted.
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
        if (ev.IsHandled)
        {
            return;
        }
        _handleSelfInputEvent(ev);
        if (ev.IsHandled)
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


    public Widget? FindFirstDefaultFocussedChild(Widget wTop)
    {
        return FindNextChild(wTop, OffsetOrientation.DontCare, 1,
            w =>
                w.FocusState == FocusStates.Focussable
                && (w.GetAttr("defaultFocus", "false") == "true"
                || w._evaluateProperty("isDefaultFocus") is true )
        );
    }
    

    public Widget? FindFirstFocussableChild()
    {
        return FindOffsetFocussableChild(this, OffsetOrientation.DontCare, 1);
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
    public Widget? FindNextChild(Widget wCurrent, OffsetOrientation ori, int dir, Func<Widget, bool> condition)
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
        if (ori == OffsetOrientation.DontCare 
            || wCurrent.GetSelector() == OffsetOrientation.DontCare 
            || wCurrent.GetSelector() == ori)
        {
            var currentChildren = wCurrent.Children;
            if (null != currentChildren && currentChildren.Count > 0)
            {
                var wFirstChild = currentChildren[dir>0?0:currentChildren.Count-1];
                if (condition(wFirstChild))
                {
                    return wFirstChild;
                }
                
                wMatch = FindNextChild(wFirstChild, ori, dir, condition);
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
            var wSibling = wCurrentParent._findSibling(wCurrent, ori, dir);
            if (wSibling != null)
            {
                if (condition(wSibling))
                {
                    return wSibling;
                }

                wMatch = FindNextChild(wSibling, ori, dir, condition);
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
            /*
             * If the parent of the current is the start one, terminate the search. 
             */
            if (this == wCurrentParent)
            {
                return null;
            }
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

                Widget? wParentSibling = wParentParent._findSibling(wCurrentParent, ori, dir);

                if (null != wParentSibling)
                {
                    if (condition(wParentSibling))
                    {
                        return wParentSibling;
                    }
                    wMatch = FindNextChild(wParentSibling, ori, dir, condition);
                    if (wMatch != null)
                    {
                        return wMatch;
                    }
                    else
                    {
                        wCurrentParent = wParentParent;
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


    public Widget? FindOffsetFocussableChild(Widget? wStart, OffsetOrientation ori, int dir)
    {
        return FindNextChild(wStart, ori, dir, w => w.FocusState == FocusStates.Focussable);
    }


    /**
     * Focus the next/prev focussable child, if the current focus is
     * within this parent.
     * If it is not, select the first / last focussable child.
     */
    private Widget? _findOffsetFocus(OffsetOrientation ori, int dir)
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
            if (null != wOldFocus)
            {
                Widget? wNewFocus = FindOffsetFocussableChild(wOldFocus, ori, dir);
                if (null != wNewFocus)
                {
                    return wNewFocus;
                }
            }
        }

        return FindOffsetFocussableChild(this, ori, dir);
    }
    
    
    /**
     * Perform the movement when we shall select the next or previous
     * of my siblings, like navigating with a tab in a dialog.
     */
    private void _setOffsetFocus(OffsetOrientation ori, int dir)
    {
        RootWidget? wRoot = Root;
        if (null == wRoot)
        {
            return;
        }

        Widget? wNewFocus = _findOffsetFocus(ori, dir);
        if (wNewFocus != null)
        {
            wRoot.SetFocussedChild(wNewFocus);
        }
    }


    /**
     * Perform the navigation like when navigating in a menu.
     * This works like focus my previous or next direct child.
     */
    private Widget? _findMenuFocus(int dir)
    {
        RootWidget? wRoot = Root;
        if (null == wRoot)
        {
            return null;
        }

        ImmutableList<Widget> children = Children;
        if (null == children)
        {
            return null;
        }

        int l = children.Count;
        if (l <= 1)
        {
            if (0 == l)
            {
                return null;
            }
            return children[0];
        }

        Widget? wOldFocus = wRoot.GetFocussedChild();
        Widget? wMyOldFocussedChild = null;

        {
            /*
             * Find my child that is ancestor of the currently focussed widget.
             */
            if (null != wOldFocus)
            {
                wMyOldFocussedChild = FindMyChild(wOldFocus);
            }
        }
        if (wMyOldFocussedChild != null)
        {
            /*
             * Find the next/previous direct child.
             */
            int oldIndex = children.IndexOf(wMyOldFocussedChild);
            if (-1 != oldIndex)
            {
                for (int i = 1; i < l; i++)
                {
                    int newIndex = (oldIndex + Int32.Sign(dir) * i + l)%l;
                    
                    /*
                     * Now, to select this item, we would need to find the first
                     * focussable child.
                     */
                    Widget wSibling = children[newIndex];
                    Widget? wFirstFocussable = wSibling.FindFirstFocussableChild();
                    if (null != wFirstFocussable)
                    {
                        return wFirstFocussable;
                    }
                }
            }
        }
        
        {
            /*
             * Select the last / first child
             */
            if (dir > 0)
            {
                return children[l-1];
            }
            else
            {
                return children[0];
            }
        }
    } 
    
    
    private void _setMenuFocus(int dir)
    {
        RootWidget? wRoot = Root;
        if (null == wRoot)
        {
            return;
        }

        Widget? wNewFocus = _findMenuFocus(dir);
        if (wNewFocus != null)
        {
            wRoot.SetFocussedChild(wNewFocus);
        }
    }
    

    public void OnInit()
    {
        _emitEvent("onInit");
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