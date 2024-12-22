using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using static engine.Logger;

namespace builtin.jt;

class FocusForEntry
{
    public string TargetId;
    public List<string> ProxyIdList = new();
}

/**
 * Top level widget that associates a widget with a factory.
 */
public class RootWidget : Widget
{
    private SortedDictionary<string, Widget> _idMap = new();
    private Widget? _wFocussedChild = null;

    private SortedDictionary<string, FocusForEntry> _mapFocusFor = new();
    
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


    public IReadOnlyList<string>? GetFocussedFor(string strTargetId)
    {
        lock (_lo)
        {
            if (_mapFocusFor.TryGetValue(strTargetId, out var ffe))
            {
                if (ffe.ProxyIdList.Count > 0)
                {
                    return ffe.ProxyIdList.ToImmutableList();
                }
            }
        }

        return null;
    }


    public void AddFocusFor(string strTargetId, string strProxyId)
    {
        FocusForEntry ffe;
        lock (_lo)
        {
            if (!_mapFocusFor.TryGetValue(strTargetId, out ffe))
            {
                ffe = new FocusForEntry() { TargetId = strTargetId };
            }

            // Trace($"Added focus for target {strTargetId} proxy {strProxyId}");
            ffe.ProxyIdList.Add(strProxyId);
            _mapFocusFor.Add(strTargetId, ffe);
        }
    }


    public void RemoveFocusFor(string strTargetId, string strProxyId)
    {
        FocusForEntry ffe;
        lock (_lo)
        {
            if (!_mapFocusFor.TryGetValue(strTargetId, out ffe))
            {
                Error($"Unable to RemoveFocusFor target {strTargetId}, proxy {strProxyId}: Target not found.");
                return;
            }

            if (!ffe.ProxyIdList.Remove(strProxyId))
            {
                Error($"Unable to remove target {strTargetId}, proxy {strProxyId}: Proxy not found.");
                return;
            }
            // Trace($"Removed focus for target {strTargetId} proxy {strProxyId}");
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
                if (_idMap.TryGetValue(id, out var wOldWithId))
                {
                    if (wOldWithId == wChild)
                    {
                        /*
                        * Well, we most probably are subject to self-recursion.
                        * While technically not 100% correct, because this still
                        * could be a wrong double register, just ignore that
                        * call.
                        */
                    }
                    else
                    {
                        Error($"Unable to add widget with id \"{id}\", id already in use.");
                    }
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

