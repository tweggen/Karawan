using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using engine;
using engine.draw;
using engine.draw.components;
using engine.news;

namespace builtin.jt;


public class TextWidgetImplementation : IWidgetImplementation
{
    internal class AttributeEntry
    {
        public required string Name;
        public required object DefaultValue;
        public required Action<TextWidgetImplementation, AttributeEntry, object> ApplyFunction;
    }

    private DefaultEcs.Entity _eText;
    private Widget _widget;

    private HAlign _hAlign(object oAlign)
    {
        if (null == oAlign)
        {
            return HAlign.Left;
        }
        switch ((string)oAlign)
        {
            default:
            case "left": return HAlign.Left;
            case "center":
            case "middle": return HAlign.Center;
            case "right": return HAlign.Right;
        }
    }
    
    
    public VAlign _vAlign(object oAlign)
    {
        if (null == oAlign)
        {
            return VAlign.Top;
        }
        switch ((string)oAlign)
        {
            default:
            case "top": return VAlign.Top;
            case "center":
            case "middle": return VAlign.Center;
            case "bottom": return VAlign.Bottom;
        }
    }


    public uint _color(object oColor)
    {
        if (null == oColor) return 0xff000000;
        string strColor = (string)oColor;
        if (String.IsNullOrEmpty(strColor)) return 0xff000000;
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


    private float _x(object oX) => (float)oX;
    private float _y(object oY) => (float)oY;
    private float _width(object oWidth) => (float)oWidth;
    private float _height(object oHeight) => (float)oHeight;
    private string _text(object oText) => (string)oText;

    
    private void _updateColor()
    {
        string strColor = _widget.GetAttr("color", "#ff000000");
        uint color = _color(strColor);

        uint finalColor = _widget.IsFocussed ? 0xffffffff : _widget.IsSelected ? 0xffffff00 : color;
        _eText.Get<OSDText>().TextColor = finalColor;
    }


    private void _onSetText(string text)
    {
        ref var cOSDText = ref _eText.Get<OSDText>();
        cOSDText.Text = text;
    }
    

    private SortedDictionary<string, AttributeEntry> _mapAttributes = new()
    {
        {
            "x", new()
            {
                Name = "x", DefaultValue = (object)0f, ApplyFunction = (impl, ae, o) =>
                {
                    impl._eText.Get<OSDText>().Position.X = impl._x(o);
                }
            }
        },
        {
            "y", new()
            {
                Name = "y", DefaultValue = (object)0f, ApplyFunction = (impl, ae, o) =>
                {
                    impl._eText.Get<OSDText>().Position.Y = impl._y(o);
                }
            }
        },
        {
            "width", new()
            {
                Name = "width", DefaultValue = (object)0f, ApplyFunction = (impl, ae, o) =>
                {
                    impl._eText.Get<OSDText>().Size.X = impl._width(o);
                }
            }
        },
        {
            "height", new()
            {
                Name = "height", DefaultValue = (object)0f, ApplyFunction = (impl, ae, o) =>
                {
                    impl._eText.Get<OSDText>().Size.X = impl._height(o);
                }
            }
        },
        {
            "hAlign", new()
            {
                Name = "hAlign", DefaultValue = (object)"left", ApplyFunction = (impl, ae, o) =>
                {
                    impl._eText.Get<OSDText>().HAlign = impl._hAlign(o);
                }
            }
        },
        {
            "vAlign", new()
            {
                Name = "vAlign", DefaultValue = (object)"top", ApplyFunction = (impl, ae, o) =>
                {
                    impl._eText.Get<OSDText>().VAlign = impl._vAlign(o);
                }
            }
        },
        {
            "fillColor", new()
            {
                Name = "fillColor", DefaultValue = (object)"0xff888888", ApplyFunction = (impl, ae, o) =>
                {
                    impl._eText.Get<OSDText>().FillColor = impl._color(o);
                }
            }
        },
        {
            "color", new()
            {
                Name = "color", DefaultValue = (object)"0xffffffff", ApplyFunction = (impl, ae, o) =>
                {
                    impl._updateColor();
                }
            }
        },
        {
            "focussed", new()
            {
                Name = "focussed", DefaultValue = (object) false, ApplyFunction = (impl, ae, o) =>
                {
                    impl._updateColor();
                }
            }
        },
        {
            "selected", new()
            {
                Name = "selected", DefaultValue = (object) false, ApplyFunction = (impl, ae, o) =>
                {
                    impl._updateColor();
                }
            }
        },
        {
            "visible", new()
            {
                Name = "visible", DefaultValue = (object) true, ApplyFunction = (impl, ae, o) =>
                {
                }
            }
        },
        {
            "text", new ()
            {
                Name = "text", DefaultValue = (object) "", ApplyFunction = (impl, ae, o) =>
                {
                    impl._onSetText(impl._text(o));
                } 
            }
        }
    };


    public void OnPropertyChanged(string key, object oldValue, object newValue)
    {
        if (_mapAttributes.TryGetValue(key, out var ae))
        {
            ae.ApplyFunction(this, ae, newValue);
        }
        else
        {
            // I don't know that property.
        }
    }

    
    public void Unrealize()
    {
        if (_eText.IsAlive)
        {
            _eText.Dispose();
        }
    }


    public void Dispose()
    {
        Unrealize();
        _widget = null;
    }

    
    public TextWidgetImplementation(Widget w)
    {
        _widget = w;
        _eText = I.Get<Engine>().CreateEntity("widget");
        string text = (string)w["text"];
        _eText.Set(new OSDText()
        {
            HAlign = HAlign.Left,
            VAlign = VAlign.Top,
            Position = new Vector2( (float) w["x"], (float) w["y"] ),
            Size = new Vector2( (float) w["width"], (float) w["height"] ),
            Text = text,
            FontSize = 16,
            TextColor = 0xffffff00,
            FillColor = 0xff0000ff
        });
        /*
         * Go to the proper callback to setup the additional properties.
         */
        _onSetText(text);
        _eText.Set(new engine.behave.components.Clickable()
        {
            ClickEventFactory = (e, cev, v2RelPos) => new WidgetEvent($"builtin.jt.widget.{_widget.Id}.onClick", this._widget) { RelativePosition = v2RelPos}
        });
        foreach (var kvp in _mapAttributes)
        {
            kvp.Value.ApplyFunction(this, kvp.Value, w.GetAttr(kvp.Value.Name, kvp.Value.DefaultValue));
        }
    }
}