using System.Numerics;
using engine;
using engine.draw;
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
                eText.Get<OSDText>().Position.X = (float) newValue;
                break;
            case "y":
                eText.Get<OSDText>().Position.Y = (float) newValue;
                break;
            case "width":
                eText.Get<OSDText>().Size.X = (float) newValue;
                break;
            case "height":
                eText.Get<OSDText>().Size.Y = (float) newValue;
                break;
            default:
                break;
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
            HAlign = HAlign.Left,
            VAlign = VAlign.Top,
            Position = new Vector2( (float) w["x"], (float) w["y"] ),
            Size = new Vector2( (float) w["width"], (float) w["height"] ),
            Text = (string) w["text"],
            FontSize = 16,
            TextColor = 0xffffff00,
            FillColor = 0xff0000ff
        });
    }
}