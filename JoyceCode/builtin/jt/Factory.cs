namespace builtin.jt;

public class Factory
{
    private readonly RootWidget _wRoot;

    
    public void Unrealize(Widget widget, IWidgetImplementation impl)
    {
        
    }


    public RootWidget FindRootWidget()
    {
        return _wRoot;
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
        _wRoot = new RootWidget() { Factory = this, Type = "Root"};
    }
}
