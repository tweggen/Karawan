using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using engine.gongzuo;
using System.Xml;
using engine;
using NLua;
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


public class StructureDescriptor
{
    public Action<XmlNode, Widget> BuildAction;
}


public class Parser
{
    private XmlDocument _xDoc;
    private Lazy<CompiledCache> _compiledCache = new(() => new CompiledCache());

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
                WidgetType = typeof(builtin.jt.InputWidget),
                ParentType = "text",
                TemplateProperties = new ()
                {
                    { "focussable", true },
                    { "text", "" },
                    { "cursorPos", -1 }
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
        },
        {
            "vmenu", new()
            {
                WidgetType = typeof(builtin.jt.Widget),
                ParentType = "vbox",
                TemplateProperties = new()
                {
                    { "selector", "vertical" },
                    { "focussable", false }
                },
            }
        },
        {
            "hmenu", new()
            {
                WidgetType = typeof(builtin.jt.Widget),
                ParentType = "hbox",
                TemplateProperties = new()
                {
                    { "selector", "horizontal" },
                    { "focussable", false }
                }
            }
        },
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


    public bool TryGetTDesc(XmlElement xWidget, out TypeDescriptor tdesc)
    {
        string strType = xWidget.LocalName;
        return _mapTypes.TryGetValue(strType, out tdesc);
    }
    
    
    public Widget BuildSelfWidget(XmlElement xWidget, out TypeDescriptor tdesc)
    {
        string strType = xWidget.LocalName;
        
        if (!TryGetTDesc(xWidget, out tdesc))
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
        
        w.OnInit();
        
        return w;
    }


    public void BuildText(XmlText xText, Widget wParent)
    {
    }

    private List<SortedDictionary<string, object>> _needListOfMaps(object lseLuaItems)
    {
        if (null == lseLuaItems)
        {
            return null;
        }
        if (lseLuaItems.GetType() == typeof(List<SortedDictionary<string, object>>))
        {
            return lseLuaItems as List<SortedDictionary<string, object>>;
        }

        // TXWTODO: Use Enumerable or other interfaces.
        
        {
            LuaTable ltTop = lseLuaItems as LuaTable;
            if (null == ltTop) return null;

            List<SortedDictionary<string, object>> listOfMaps = new();
            foreach (var listItem in ltTop.Values)
            {
                {
                    LuaTable ltItem = listItem as LuaTable;
                    if (ltItem != null)
                    {
                        SortedDictionary<string, object> dictProps = new();
                        foreach (var itemKey in ltItem.Keys)
                        {
                            string strItemKey = itemKey as string;
                            if (strItemKey != null)
                            {
                                dictProps.Add(strItemKey, ltItem[itemKey]);
                            }
                        }

                        listOfMaps.Add(dictProps);
                    }
                } 
            }

            return listOfMaps;
        }
        
    }
    
    
    public void BuildChildElement(XmlElement xWidget, Widget wParent, LuaBindingFrame? lbfParent)
    {
        string strNode = xWidget.LocalName;
        Widget w;
        int nFor = 0;

        switch (strNode)
        {
            case "for":
                if (xWidget.HasAttribute("items"))
                {
                    string strItems = xWidget.GetAttribute("items");
                    var l = strItems.Length;
                    ++nFor;
                    if (l > 2 && strItems[0] == '{' && strItems[1] != '{' && strItems[l - 1] == '}')
                    {
                        var lseItemArray = _compiledCache.Value.Find(
                            $"{wParent.Id}.for#{nFor}",
                            strItems.Substring(1, l - 2),
                            (lse) => PushContext(lse));

                        var listOfMaps = _needListOfMaps(lseItemArray.CallSingleResult());

                        if (listOfMaps != null)
                        {
                            foreach (var item in listOfMaps)
                            {
                                LuaBindingFrame? lbfWidget = null;

                                if (item.Count != 0)
                                {
                                    lbfWidget = new() { MapBindings = item.ToFrozenDictionary() };
                                }
                                foreach (XmlNode xnChild in xWidget.ChildNodes)
                                {
                                    BuildChildNode(xnChild, wParent, lbfWidget);
                                }
                            }
                        }

                    }
                }
                break;
            case "if":
                break;
            default:
                w = BuildWidget(xWidget, lbfParent);
                wParent.AddChild(w);
                break;
        }
    }


    public void BuildChildNode(XmlNode xnChild, Widget wParent, LuaBindingFrame lbfWidget)
    {
        switch (xnChild.NodeType)
        {
            case XmlNodeType.None:
            case XmlNodeType.Attribute:
                break;
            case XmlNodeType.Text:
                BuildText(xnChild as XmlText, wParent);
                break;
            case XmlNodeType.Element:
                BuildChildElement(xnChild as XmlElement, wParent, lbfWidget);
                break;
        }
    }


    public void BuildChildrenElements(XmlElement xParent, Widget wParent, LuaBindingFrame? lbfParent)
    {
        /*
         * Then iterate through its children.
         */
        foreach (XmlNode xnChild in xParent.ChildNodes)
        {
            BuildChildNode(xnChild, wParent, lbfParent);
        }
    }


    public Widget? BuildWidget(XmlElement xWidget, LuaBindingFrame? lbfParent)
    {
        /*
         * Create the widget on its own
         */
        Widget w = BuildSelfWidget(xWidget, out var tdesc);
        if (lbfParent != null)
        {
            w.BuildLuaBindingFrame = lbfParent;
        }

        BuildChildrenElements(xWidget, w, lbfParent);
        
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


    internal void PushContext(LuaScriptEntry lse)
    {
        /*
         * We need the bindings from the engine...
         */
        I.Get<engine.gongzuo.API>().PushBindings(lse);

        PushBindings(lse);
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

        return BuildWidget(xWidget, null);
    }
        
    
    public Parser(XmlDocument xDoc, Factory factory)
    {
        _xDoc = xDoc;
        _factory = factory;
        _lbf = new()
        {
            MapBindings = new SortedDictionary<string, object>()
            {
                { "jt", new JtBindings(factory,this) }
            }.ToFrozenDictionary()
        };
    }
}