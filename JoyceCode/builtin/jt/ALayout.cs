using System.Collections.Generic;

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


    protected virtual void _removeItem(ALayoutItem wChild)
    {
        lock (_lo)
        {
            _listLayoutItems.Remove(wChild);
        }
    }
    

    protected virtual void _addItem(ALayoutItem wChild)
    {
        lock (_lo)
        {
            _listLayoutItems.Add(wChild);
        }
    }
    

    public virtual void Activate()
    {
    }
}