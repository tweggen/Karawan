using engine;
using engine.draw.components;

namespace builtin.jt;

public class TextWidgetImplementation : IWidgetImplementation
{
    private DefaultEcs.Entity eText;
    
    public void OnPropertyChanged(string key, object oldValue, object newValue)
    {
        switch (key)
        {
            case "x":
            case "y":
            case "width":
            case "height":
            default:
        }
    }

    public void Unrealize()
    {
        eText.Dispose();
    }

    public TextWidgetImplementation(Widget w)
    {
        eText = I.Get<Engine>().CreateEntity("widget");
        eText.Set(new OSDText()
        {
            
        })
    }
}