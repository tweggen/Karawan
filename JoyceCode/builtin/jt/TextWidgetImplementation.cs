using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using engine;
using engine.draw;
using engine.draw.components;

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
    protected Widget _widget;

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

    protected virtual void _computeOsdText(ref OSDText cOsdText)
    {
        bool isVisuallyFocussed = _widget.IsVisuallyFocussed;
        
        {
            string strColor = _widget.GetAttr("color", "#ff000000");
            uint color = _color(strColor);

            uint finalTextColor = isVisuallyFocussed ? 0xff22aaee : _widget.IsSelected ? 0xff22aa55 : color;
            uint finalFillColor = isVisuallyFocussed ? 0xff777777 : 0x00000000;

            cOsdText.TextColor = finalTextColor;
            cOsdText.FillColor = finalFillColor;
        }
        cOsdText.Position.X = _x(_widget.GetAttr("x", 0f));
        cOsdText.Position.Y = _y(_widget.GetAttr("y", 0f));
        cOsdText.Size.X = _width(_widget.GetAttr("width", 0f));
        cOsdText.Size.Y = _height(_widget.GetAttr("height", 0f));
        cOsdText.HAlign = _hAlign(_widget.GetAttr("hAlign", "Left"));
        cOsdText.VAlign = _vAlign(_widget.GetAttr("vAlign", "Top"));
        cOsdText.FillColor = _color(_widget.GetAttr("fillColor", "#00000000"));

        {
            string realText = _widget.GetAttr("text", "");
            if (_widget.GetAttr("type", "") == "password")
            {
                if (realText.Length > 1)
                {
                    realText = new string('*', realText.Length-1) + realText.Substring(realText.Length-1);
                }
                else
                {
                    // Leave it.
                }
            }
            
            cOsdText.Text = realText;
        }
    }
    
    
    protected void _updateOsdText()
    {
        ref var cOsdText = ref _eText.Get<OSDText>();
        _computeOsdText(ref cOsdText);
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
                    impl._updateOsdText();
                }
            }
        },
        {
            "y", new()
            {
                Name = "y", DefaultValue = (object)0f, ApplyFunction = (impl, ae, o) =>
                {
                    impl._updateOsdText();
                }
            }
        },
        {
            "width", new()
            {
                Name = "width", DefaultValue = (object)0f, ApplyFunction = (impl, ae, o) =>
                {
                    impl._updateOsdText();
                }
            }
        },
        {
            "height", new()
            {
                Name = "height", DefaultValue = (object)0f, ApplyFunction = (impl, ae, o) =>
                {
                    impl._updateOsdText();
                }
            }
        },
        {
            "hAlign", new()
            {
                Name = "hAlign", DefaultValue = (object)"left", ApplyFunction = (impl, ae, o) =>
                {
                    impl._updateOsdText();
                }
            }
        },
        {
            "vAlign", new()
            {
                Name = "vAlign", DefaultValue = (object)"top", ApplyFunction = (impl, ae, o) =>
                {
                    impl._updateOsdText();
                }
            }
        },
        {
            "fillColor", new()
            {
                Name = "fillColor", DefaultValue = (object)"0xff888888", ApplyFunction = (impl, ae, o) =>
                {
                    impl._updateOsdText();
                }
            }
        },
        {
            "color", new()
            {
                Name = "color", DefaultValue = (object)"0xffffffff", ApplyFunction = (impl, ae, o) =>
                {
                    impl._updateOsdText();
                }
            }
        },
        {
            "focussed", new()
            {
                Name = "focussed", DefaultValue = (object)false, ApplyFunction = (impl, ae, o) =>
                {
                    impl._updateOsdText();
                    impl._updateFocussedFor();
                }
            }
        },
        {
            "selected", new()
            {
                Name = "selected", DefaultValue = (object)false, ApplyFunction = (impl, ae, o) =>
                {
                    impl._updateOsdText();
                }
            }
        },
        {
            "visible", new()
            {
                Name = "visible", DefaultValue = (object)true, ApplyFunction = (impl, ae, o) =>
                {
                }
            }
        },
        {
            "text", new()
            {
                Name = "text", DefaultValue = (object)"", ApplyFunction = (impl, ae, o) =>
                {
                    impl._updateOsdText();
                }
            }
        },
    };


    public virtual void OnPropertyChanged(string key, object oldValue, object newValue)
    {
        if (_mapAttributes.TryGetValue(key, out var ae))
        {
            ae.ApplyFunction(this, ae, newValue);
        }
    }

    
    public virtual void Unrealize()
    {
        if (_eText.IsAlive)
        {
            _eText.Dispose();
        }
    }


    public virtual void Dispose()
    {
        Unrealize();
        _widget = null;
    }


    private void _updateFocussedFor()
    {
        var w = _widget;
        var wRoot = w.Root;
        if (null == wRoot) return;
        IReadOnlyList<string>? listFocussedFor = wRoot.GetFocussedFor(w.GetAttr("id", ""));
        if (null == listFocussedFor) return;
        foreach (var strProxyId in listFocussedFor)
        {
            if (wRoot.GetChild(strProxyId, out var wProxy))
            {
                builtin.jt.ImplementationFactory? factory = wRoot.ImplementationFactory;
                if (factory != null)
                {
                    if (factory.TryGetImplementation(wProxy, out var impl))
                    {
                        if (impl != this)
                        {
                            var twi = impl as TextWidgetImplementation;
                            if (null != twi)
                            {
                                twi._updateOsdText();
                            }
                        }
                    }
                }
            }
        }
    }


    protected void _defaultOsdText(ref OSDText osdText)
    {
        var w = _widget;
        osdText.HAlign = HAlign.Left;
        osdText.VAlign = VAlign.Top;
        osdText.Position = new Vector2((float)w["x"], (float)w["y"]);
        osdText.Size = new Vector2((float)w["width"], (float)w["height"]);
        osdText.Text = _text(w.GetAttr("text",""));
        osdText.FontSize = 17;
        osdText.TextColor = 0xffffff00;
        osdText.FillColor = 0xff0000ff;
        osdText.GaugeColor = 0xff22cccc;
        osdText.GaugeValue = 0;
    }
    
    
    public TextWidgetImplementation(Widget w)
    {
        _widget = w;
        _eText = I.Get<Engine>().CreateEntity("widget");
        OSDText cOsdText = new();
        _defaultOsdText(ref cOsdText);
        _computeOsdText(ref cOsdText);

        _eText.Set(cOsdText);
        _eText.Set(new engine.behave.components.Clickable()
        {
            ClickEventFactory = (e, cev, v2RelPos) =>
                new WidgetEvent(
                    cev.IsPressed?$"builtin.jt.widget.{_widget.Id}.onClick":$"builtin.jt.widget.{_widget.Id}.onReleased", 
                    this._widget) { RelativePosition = v2RelPos}
                 
        });
        foreach (var kvp in _mapAttributes)
        {
            kvp.Value.ApplyFunction(this, kvp.Value, w.GetAttr(kvp.Value.Name, kvp.Value.DefaultValue));
        }
    }
}