namespace builtin.jt;

public class Factory
{
    public void Unrealize(Widget widget, IWidgetImplementation impl)
    {
        
    }
    
    
    /**
     * Create the platform specific implementation for the widget.
     * This implementation may be null.
     */
    public IWidgetImplementation? Realize(Widget w)
    {
        switch (w.Type)
        {
            case "Text":
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
}