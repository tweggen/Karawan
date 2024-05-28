using System.Collections.Generic;

namespace builtin.jt;

public class Factory
{
    private SortedDictionary<string, RootWidget> _mapLayers = new();

    
    public void Unrealize(Widget widget, IWidgetImplementation impl)
    {
        
    }


    public RootWidget FindRootWidget(string layername)
    {
        RootWidget? wRoot;
        if (_mapLayers.TryGetValue(layername, out wRoot))
        {
        }
        else
        {
            wRoot = new RootWidget() { Factory = this, Type = "Root"};
            _mapLayers[layername] = wRoot;
        }

        return wRoot;
    }


    public (float, float) GetTextExtent(object font, string text)
    {
        // TXWTODO: This is a wild guess. We would need to establish some interface for that.
        return (12*text.Length, 20);
    }
    

    /**
     * Create the platform specific implementation for the widget.
     * This implementation may be null.
     */
    public IWidgetImplementation? Realize(Widget w)
    {
        switch (w.Type)
        {
            case "text":
            case "option":
                /*
                 * Text is interpreted as an OSDText entity.
                 */
                return new TextWidgetImplementation(w);
                break;
            
            case "input":
                return new InputWidgetImplementation(w);
                break;
            
            default:
                /*
                 * Everything is just nothing.
                 */
                break;
        }

        return null;
    }
    

    public Factory()
    {
    }
}
