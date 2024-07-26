using engine.draw.components;

namespace builtin.jt;

public class InputWidgetImplementation : TextWidgetImplementation
{
    protected override void _computeOsdText(ref OSDText cOsdText)
    {
        bool isFocussed = _widget.IsFocussed;
        base._computeOsdText(ref cOsdText);

        int cursorPos = (_widget as InputWidget)._cursorPos();
        float pos = cursorPos; 
        cOsdText.GaugeValue = (ushort)pos;
        cOsdText.GaugeColor = isFocussed ? 0xff00ffff:0x00000000;
        cOsdText.OSDTextFlags = (ushort) ((cOsdText.OSDTextFlags) & (~OSDText.GAUGE_TYPE_MASK) | (OSDText.GAUGE_TYPE_INSERT));
        cOsdText.BorderColor = isFocussed ? cOsdText.TextColor : cOsdText.FillColor;
    }


    public override void OnPropertyChanged(string key, object oldValue, object newValue)
    {
        switch (key)
        {
            case "cursorPos":
                _updateOsdText();
                break;
            default:
                base.OnPropertyChanged(key, oldValue, newValue);
                break;
        }
    }

    
    public override void Dispose()
    {
        base.Dispose();
    }
    
    
    public override void Unrealize()
    {
        base.Unrealize();
    }
    
    
    public InputWidgetImplementation(Widget w) : base(w)
    {
    }
}