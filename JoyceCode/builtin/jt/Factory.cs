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

    /**
     * Create the platform specific implementation for the widget.
     * This implementation may be null.
     */
    public IWidgetImplementation? Realize(Widget w)
    {
        switch (w.Type)
        {
            case "text":
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
        _wRoot = new RootWidget() { Factory = this, Type = "Root" };
    }
}