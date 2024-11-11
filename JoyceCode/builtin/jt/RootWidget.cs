using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using static engine.Logger;

namespace builtin.jt;

/**
 * Top level widget that associates a widget with a factory.
 */
public class RootWidget : Widget
{
    private SortedDictionary<string, Widget> _idMap = new();
    private Widget? _wFocussedChild = null;
    
    private ImplementationFactory _implementationFactory; 
    public required ImplementationFactory ImplementationFactory
    {
        set
        {
            lock (_lo)
            {
                _implementationFactory = value;
            }
        }
        get
        {
            lock (_lo)
            {
                return _implementationFactory;
            }
        }
    }


    public Widget? GetFocussedChild()
    {
        lock (_lo)
        {
            return _wFocussedChild; 
        }
    }


    public void SetFocussedChild(Widget? wFocussed)
    {
        Widget? wOldFocus;
        
        lock (_lo)
        {
            wOldFocus = _wFocussedChild;
            
            /*
             * Note that both pointers very well may be null.
             */
            if (wFocussed == wOldFocus)
            {
                return;
            }
            
            /*
             * Otherwise first onfocus the former, then focus the new.
             */
        }

        if (wOldFocus != null)
        {
            wOldFocus._unfocusSelf();
            lock (_lo)
            {
                _wFocussedChild = null;
            }
        }

        if (wFocussed != null)
        {
            lock (_lo)
            {
                _wFocussedChild = wFocussed;
            }

            wFocussed._focusSelf();
        }
        
    }


    /**
     * Unfocus a child.
     * This child might not have been focussed at all from the root widget's point
     * of view. So only really onfocus it, if it really was focussed. Otherwise,
     * ignore the call.
     */
    public void UnfocusChild(Widget wFocussed)
    {
        lock (_lo)
        {
            if (_wFocussedChild != wFocussed)
            {
                return;
            }
        }
        SetFocussedChild(null);
    }


    public bool GetChild(string id, [MaybeNullWhen(false)] out Widget widget)
    {
        if (string.IsNullOrEmpty(id))
        {
            Error($"Unable to find a child by empty id.");
            widget = null;
            return false;
        }

        lock (_lo)
        {
            return _idMap.TryGetValue(id, out widget);
        }
    }

    
    public override void RemoveChild(Widget child)
    {
        string id = child.GetAttr("id", "");
        Widget wCurrentlyFocussedChild;
        lock (_lo)
        {
            if (!string.IsNullOrEmpty(id))
            {
                if (!_idMap.Remove(id))
                {
                    Error($"Warning, no widget with id \"{id}\" could be found.");
                }
            }

            wCurrentlyFocussedChild = _wFocussedChild;
        }
        
        /*
         * Remove the widget from things it might be involved in.
         */
        if (wCurrentlyFocussedChild == child)
        {
            SetFocussedChild(null);
        }
        
        base.RemoveChild(child);
        child.Root = null;
    }


    internal void RegisterChild(Widget wChild)
    {
        string id = wChild.GetAttr("id", "");
        lock (_lo)
        {
            if (!string.IsNullOrEmpty(id))
            {
                if (_idMap.TryGetValue(id, out var _))
                {
                    Error($"Unable to add widget with id \"{id}\", id already in use.");
                }
                else
                {
                    _idMap[id] = wChild;
                }
            }
        }
        wChild.Root = this;

        if (wChild.IsFocussed)
        {
            SetFocussedChild(wChild);
        }
    }
    

    public override void AddChild(Widget wChild)
    {
        base.AddChild(wChild);

        RootWidget wItsRoot = null;
        lock (_lo)
        {
            wItsRoot = wChild.Root;
        }

        if (null == wItsRoot)
        {
            RegisterChild(wChild);
        }
    }


    public override void PropagateInputEvent(engine.news.Event ev)
    {
        Widget? wFocussedChild;
        lock (_lo)
        {
            wFocussedChild = _wFocussedChild;
        }

        wFocussedChild?.PropagateInputEvent(ev);
    }
}

