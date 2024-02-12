using System.Globalization;
using System.Numerics;
using engine;
using engine.draw;
using engine.draw.components;
using ObjLoader.Loader.Common;

namespace builtin.jt;

public class TextWidgetImplementation : IWidgetImplementation
{
    private DefaultEcs.Entity eText;
    private Widget _widget;

    public HAlign _hAlign(string strAlign)
    {
        if (null == strAlign)
        {
            return HAlign.Left;
        }
        switch (strAlign)
        {
            default:
            case "left": return HAlign.Left;
            case "center":
            case "middle": return HAlign.Center;
            case "right": return HAlign.Right;
        }
    }
    
    
    public VAlign _vAlign(string strAlign)
    {
        if (null == strAlign)
        {
            return VAlign.Top;
        }
        switch (strAlign)
        {
            default:
            case "top": return VAlign.Top;
            case "center":
            case "middle": return VAlign.Center;
            case "bottom": return VAlign.Bottom;
        }
    }


    public uint _color(string strColor)
    {
        if (strColor.IsNullOrEmpty()) return 0xff000000;
        if (strColor[0] == '#')
        {
            if (uint.TryParse(
                    strColor.Substring(1),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out var color))
            {
                if (strColor.Length <= 5)
                {
                    /*
                     * 12 / 16 bit color
                     */

                    if (strColor.Length < 5)
                    {
                        /*
                         * Set full alpha if not specified.
                         */
                        color |= 0xf000;
                    }
                    
                    /*
                     * make 24/32 bit from 12/16bit 
                     */
                    return 0u
                           | ((color & 0xf000) << 16)
                           | ((color & 0xf000) << 12)
                           | ((color & 0x0f00) << 12)
                           | ((color & 0x0f00) << 8)
                           | ((color & 0x00f0) << 8)
                           | ((color & 0x00f0) << 4)
                           | ((color & 0x000f) << 4)
                           | ((color & 0x000f));
                }
                else
                {
                    /*
                     * 24/32bit color
                     */
                    if (strColor.Length <= 7)
                    {
                        color |= 0xff000000;
                    }

                    return color;
                }
            }
        }

        return 0xff000000;
    }

    
    private void _updateColor()
    {
        string strColor = _widget.GetAttr("color", "#ff000000");
        uint color = _color(strColor);

        uint finalColor = _widget.IsFocussed ? 0xffffffff : _widget.IsSelected ? 0xffffff00 : color;
        eText.Get<OSDText>().TextColor = finalColor;
    }
    
    
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
            case "hAlign":
                eText.Get<OSDText>().HAlign = _hAlign((string)newValue);
                break;
            case "vAlign":
                eText.Get<OSDText>().VAlign = _vAlign((string)newValue);
                break;
            case "fillColor":
                eText.Get<OSDText>().FillColor = _color((string)newValue);
                break;
            case "color":
            case "focussed":
            case "selected":
                _updateColor();
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
        _widget = w;
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