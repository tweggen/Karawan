using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using engine.gongzuo;
using System.Xml;
using ObjLoader.Loader.Common;
using static engine.Logger;

namespace builtin.jt;


/**
 * Contains default attributes and behavior for a certain widget
 * type.
 */
public class TypeDescriptor
{
    public Type WidgetType;
    
    /**
     * The xml tag type this type inherits.
     */
    public string ParentType;
    
    /**
     * Properties to predefine for a certain xml tag.
     */
    public SortedDictionary<string, object>? TemplateProperties;
    
    /**
     * Type specific setup action for a certain xml tag.
     */
    public Action<Widget, XmlElement> TypeSetup;
}


public class Parser
{
    private XmlDocument _xDoc;


    private readonly SortedDictionary<string, Type> _mapAttributeTypes = new()
    {
        { "x", typeof(float) },
        { "y", typeof(float) },
        { "width", typeof(float) },
        { "height", typeof(float) },
        { "text", typeof(string) },
        { "nColumns", typeof(uint) },
        { "nRows", typeof(uint) },
        { "direction", typeof(string) },
        { "flex", typeof(float) },
        { "vAlign", typeof(string) },
        { "hAlign", typeof(string) },
        { "focussable", typeof(bool) },
        { "selectable", typeof(bool) }
    };


    private static void _setupAnyBox(Widget w, XmlElement xWidget)
    {
        var boxLayout = new BoxLayout() { Parent = w, IsHorizontal = w.GetAttr("direction", "vertical") == "horizontal"};
        w.Layout = boxLayout;
        var children = w.Children;
        if (null != children)
        {
            foreach (var child in children)
            {
                boxLayout.AddWidget(child, 1f);
            }
        }
    }


    private readonly SortedDictionary<string, TypeDescriptor> _mapTypes = new()
    {
        {
            "jt", new()
            {
                WidgetType = typeof(builtin.jt.Widget),
                ParentType = "view",
                TemplateProperties = new()
                {
                    { "focussable", false },
                } 
            }
        },
        {
            "view", new()
            {
                WidgetType = typeof(builtin.jt.Widget),
                ParentType = null,
                TemplateProperties = new()
                {
                    { "focussable", false },
                    { "hAlign", "left" },
                    { "vAlign", "top" }
                } 
            }
        },
        { 
            "text", new()
            {
                WidgetType = typeof(builtin.jt.TextWidget),
                ParentType = "view",
                TemplateProperties = new()
                {
                }
            }
        },
        {
            "option", new ()
            {
                WidgetType = typeof(builtin.jt.TextWidget),
                ParentType = "text",
                TemplateProperties = new ()
                {
                    { "focussable", true }
                }
            }
        },
        {
            "input", new ()
            {
                WidgetType = typeof(builtin.jt.Widget),
                ParentType = "text",
                TemplateProperties = new ()
                {
                    { "focussable", true }
                }
            }
        },
        {
            "grid", new()  
            { 
                WidgetType = typeof(builtin.jt.Widget),
                ParentType = "view",
                TemplateProperties = new()
                {
                    { "nColumns", 1 },
                    { "nRows", 1 }
                } 
            }
        },
        {
            "box", new ()
            {
                WidgetType = typeof(builtin.jt.Widget),
                ParentType = "view",
                TemplateProperties = new()
                {
                    { "direction", "vertical" }
                }, 
                TypeSetup = _setupAnyBox
            }
        },
        {
            "vbox", new()
            {
                WidgetType = typeof(builtin.jt.Widget),
                ParentType = "view",
                TemplateProperties = new()
                {
                    { "direction", "vertical" }
                },
                TypeSetup = _setupAnyBox
            }
        },
        {
            "hbox", new()
            {
                WidgetType = typeof(builtin.jt.Widget),
                ParentType = "view",
                TemplateProperties = new()
                {
                    { "direction", "horizontal" }
                }, 
                TypeSetup = _setupAnyBox
            }
        }
    };

    
    private readonly Factory _factory;
    private LuaBindingFrame _lbf;

    
    public bool IsValidType(string strType)
    {
        return _mapTypes.ContainsKey(strType);
    }


    public void ApplyTemplate(Widget w, TypeDescriptor tdesc)
    {
        /*
         * First, apply the properties of the parent.
         */
        if (tdesc.ParentType != null)
        {
            if (_mapTypes.TryGetValue(tdesc.ParentType, out var ptdesc))
            {
                ApplyTemplate(w, ptdesc);
            }
        }

        /*
         * Then, apply the properties of the item itselves.
         */
        if (tdesc.TemplateProperties != null)
        {
            foreach (var kvp in tdesc.TemplateProperties)
            {
                w[kvp.Key] = kvp.Value;
            }
        }

    }


    public void ApplyTypeSetup(Widget w, TypeDescriptor tdesc, XmlElement xWidget)
    {
        if (null != tdesc.TypeSetup)
        {
            tdesc.TypeSetup(w, xWidget);
        }

        if (tdesc.ParentType != null)
        {
            if (_mapTypes.TryGetValue(tdesc.ParentType, out var ptdesc))
            {
                ApplyTypeSetup(w, ptdesc, xWidget);
            }
        }
    }


    public void ApplyBuiltin(Widget w)
    {
        w["parser"] = this;
    }
    
    
    public Widget BuildSelfWidget(XmlElement xWidget, out TypeDescriptor tdesc)
    {
        string strType = xWidget.LocalName;
        
        if (!_mapTypes.TryGetValue(strType, out tdesc))
        {
            ErrorThrow<ArgumentException>($"Invalid type {strType}.");
            return null;
        }
        
        /*
         * First create the widget including all of the attributes.
         */
        Widget w = Activator.CreateInstance(tdesc.WidgetType) as Widget;
        w.Type = strType;
        
        ApplyTemplate(w, tdesc);
        ApplyBuiltin(w);

        /*
         * Then, the attributes from the xml.
         */
        int l = xWidget.Attributes.Count;
        for (int i=0; i<l; ++i)
        {
            var attr = xWidget.Attributes[i];
            Type typeAttr;
            if (_mapAttributeTypes.TryGetValue(attr.LocalName, out typeAttr))
            {
                try
                {
                    if (typeAttr == typeof(float))
                    {
                        w[attr.LocalName] = Single.Parse(attr.Value);
                    }
                    else if (typeAttr == typeof(string))
                    {
                        w[attr.LocalName] = attr.Value;
                    }
                    else if (typeAttr == typeof(int))
                    {
                        w[attr.LocalName] = Int32.Parse(attr.Value);
                    }
                    else if (typeAttr == typeof(uint))
                    {
                        w[attr.LocalName] = UInt32.Parse(attr.Value);
                    } 
                    else if (typeAttr == typeof(bool))
                    {
                        w[attr.LocalName] = Boolean.Parse(attr.Value);
                    }
                        
                }
                catch (Exception e)
                {
                    ErrorThrow<ArgumentException>(
                        $"Unable to cast attribute value for attribute {attr.Name} from {attr.Value} to type {typeAttr.FullName}");
                }
            }
            else
            {
                w[attr.LocalName] = attr.Value.ToString();
            }
        }

        /*
         * Parse out special attributes that map to native flags
         */
        w.FocusState = w.GetAttr("focussable", true) ? FocusStates.Focussable : FocusStates.Unfocussable;
        w.SelectionState = w.GetAttr("selectable", true) ? SelectionStates.Selectable : SelectionStates.Unselectable;
        
        /*
         * Then, the special text attribute
         */
        {
            string text = xWidget.InnerText;
            if (!text.IsNullOrEmpty())
            {
                w["text"] = text;
            }
        }
        
        return w;
    }


    public Widget BuildText(XmlText xText)
    {
        return null;
    }
    

    public Widget BuildWidget(XmlElement xWidget)
    {
        /*
         * Create the widget on its own
         */
        Widget w = BuildSelfWidget(xWidget, out var tdesc);
        
        /*
         * Then iterate through its children.
         */
        foreach (XmlNode xnChild in xWidget.ChildNodes)
        {
             
            switch (xnChild.NodeType)
            {
                case XmlNodeType.None:
                    break;
                case XmlNodeType.Element:
                    Widget wChildElement = BuildWidget(xnChild as XmlElement);
                    w.AddChild(wChildElement);
                    break;
                case XmlNodeType.Attribute:
                    break;
                case XmlNodeType.Text:
                    /*
                     * Plain text is added to the text property of the parent widget.
                     */
                    break;
            }
        }
        
        /*
         * Finally, the type specific operator, after all children and attributes
         * are setup.
         */
        ApplyTypeSetup(w, tdesc, xWidget);
        
        return w;
    }
    
    
    internal void PushBindings(LuaScriptEntry lse)
    {
        /*
         * No parent binding to call here.
         */
        lse.PushBinding(_lbf);
    }
    
    
    /**
     * Build a meaningful display from the xml input.
     *
     * Syntax: everything below root.
     */
    public Widget Build(string? id = null)
    {
        XmlElement xWidget;
        if (null == id)
        {
            xWidget = _xDoc.GetElementsByTagName("view")[0] as XmlElement;   
        }
        else
        {
            xWidget = _xDoc.SelectSingleNode($"//*[@id='{id}']") as XmlElement;
        }
        if (null == xWidget)
        {
            ErrorThrow<ArgumentException>("No widget found.");
        }

        return BuildWidget(xWidget);
    }
        
    
    public Parser(XmlDocument xDoc, Factory factory)
    {
        _xDoc = xDoc;
        _factory = factory;
        _lbf = new()
        {
            MapBindings = new SortedDictionary<string, object>()
            {
                { "jt", new JtBindings(factory,this) },
                { "joyce", new engine.gongzuo.JoyceBindings() }
            }.ToFrozenDictionary()
        };
    }
}