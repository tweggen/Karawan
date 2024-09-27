using System.Collections.Generic;

namespace builtin.jt;

public class ImplementationFactory
{
    public void Unrealize(Widget widget, IWidgetImplementation impl)
    {
        
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
        }

        return null;
    }
    

    public ImplementationFactory()
    {
    }
}
