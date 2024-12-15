using System.Collections.Generic;

namespace builtin.jt;

public class ImplementationFactory
{
    private object _lo = new();
    
    private Dictionary<Widget, IWidgetImplementation> _mapWidgetImplementations = new();
    
    public void Unrealize(Widget widget, IWidgetImplementation impl)
    {
        lock (_lo)
        {
            _mapWidgetImplementations.Remove(widget);
        }
    }


    public (float, float) GetTextExtent(object font, string text)
    {
        // TXWTODO: This is a wild guess. We would need to establish some interface for that.
        return (12*text.Length, 20);
    }


    public bool TryGetImplementation(Widget w, out IWidgetImplementation impl)
    {
        lock (_lo)
        {
            return _mapWidgetImplementations.TryGetValue(w, out impl);
        }
    }
    

    /**
     * Create the platform specific implementation for the widget.
     * This implementation may be null.
     */
    public IWidgetImplementation? Realize(Widget w)
    {
        IWidgetImplementation impl; 
        switch (w.Type)
        {
            case "text":
            case "option":
                /*
                 * Text is interpreted as an OSDText entity.
                 */
                impl = new TextWidgetImplementation(w);
                break;
            
            case "input":
                impl = new InputWidgetImplementation(w);
                break;
            
            default:
                return null;
        }

        lock (_lo)
        {
            _mapWidgetImplementations.Add(w, impl);   
        }
        return impl;
    }
    

    public ImplementationFactory()
    {
    }
}
