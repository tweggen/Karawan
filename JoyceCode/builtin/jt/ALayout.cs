using System.Collections.Generic;

namespace builtin.jt;

public class ALayout
{
    private object _lo = new();
    
    private Widget _wParent;
    public Widget Parent
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
            
            // TXWTODO: Trigger relayout if set up.
        }
    }


    private List<ALayoutItem> _listLayoutItems = new();


    abstract protected ALayoutItem _addItem(Widget wChild)
    {
        
    }

    public void Activate()
    {
    }


    public void AddWidget(Widget wChild)
    {
    }


    public void RemoveWidget(Widget wChild)
    {
    }
}