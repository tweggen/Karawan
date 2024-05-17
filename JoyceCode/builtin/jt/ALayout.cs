using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using static engine.Logger;

namespace builtin.jt;

public class ALayout
{
    private object _lo = new();

    private Widget _wParent;

    public Widget? Parent
    {
        get
        {
            lock (_lo)
            {
                return _wParent;
            }
        }
        set
        {
            Widget wOldParent;
            lock (_lo)
            {
                if (value == _wParent)
                {
                    return;
                }

                wOldParent = _wParent;
                _wParent = value;
            }

            if (null != value)
            {
                /*
                 * Trigger relayout if required.
                 */
                value.Layout = this;
                Activate();
            }
        }
    }


    private readonly List<ALayoutItem> _listLayoutItems = new();
    
    private IReadOnlyList<ALayoutItem>? _listImmutableItems;
    protected IReadOnlyList<ALayoutItem>? _immutableItemsNL()
    {
        if (null == _listLayoutItems)
        {
            return null;
        }

        if (null == _listImmutableItems)
        {
            _listImmutableItems = _listLayoutItems.ToImmutableList();
        }

        return _listImmutableItems;
    }
    

    protected IReadOnlyList<ALayoutItem>? _immutableItems()
    {
        lock (_lo)
        {
            return _immutableItemsNL();
        }
    }
    

    protected void _removeItem(Widget w)
    {
        lock (_lo)
        {
            _listImmutableItems = null;
            _listLayoutItems.RemoveAll(li => li.Widget == w);
        }
    }


    protected virtual void _removeItem(ALayoutItem wChild)
    {
        lock (_lo)
        {
            _listImmutableItems = null;
            _listLayoutItems.Remove(wChild);
        }
    }


    protected virtual void _addItem(ALayoutItem wChild)
    {
        lock (_lo)
        {
            _listImmutableItems = null;
            _listLayoutItems.Add(wChild);
        }
    }


    /**
     * Implement in client.
     * Read the layout properties, set the children's positions.-
     */
    protected virtual void _doActivate()
    {
    }


    public virtual (float Width, float Height) SizeHint()
    {
        return (0f, 0f);
    }
    

    public virtual void Activate()
    {
        lock (_lo)
        {
            if (null == _wParent)
            {
                Error($"Activated without parent.");
            }
        }

        _doActivate();
    }
}