using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
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
    public string ParentType;
    public SortedDictionary<string, object>? TemplateProperties;
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
        { "direction", typeof(string) }
    };
    
    private readonly SortedDictionary<string, TypeDescriptor> _mapTypes = new()
    {
        {
            "Widget", new()
            {
                ParentType = null,
                TemplateProperties = new()
                {
                    { "x", 0f },
                    { "y", 0f },
                    { "width", 0f },
                    { "height", 0f }
                } 
            }
        },
        {
            "Root", new()
            {
                ParentType = "Widget"
            }
        },
        { 
            "Text", new()
            {
                ParentType = "Widget"
            }
        },
        {
            "Grid", new()  
            { 
                ParentType = "Widget",
                TemplateProperties = new()
                {
                    { "nColumns", 1 },
                    { "nRows", 1 }
                } 
            }
        },
        {
            "Flex", new()
            {
                ParentType = "Widget",
                TemplateProperties = new()
                {
                    { "direction", "vertical" }
                } 
            }
        }
    };


    public bool IsValidType(string strType)
    {
        return _mapTypes.ContainsKey(strType);
    }


    public void ApplyTemplate(Widget w, TypeDescriptor tdesc)
    {
        if (tdesc.TemplateProperties != null)
        {
            /*
             * First, the attributes from the template.
             */
            foreach (var kvp in tdesc.TemplateProperties)
            {
                w[kvp.Key] = kvp.Value;
            }
        }

        if (tdesc.ParentType != null)
        {
            if (_mapTypes.TryGetValue(tdesc.ParentType, out var ptdesc))
            {
                ApplyTemplate(w, ptdesc);
            }
        }
    }

    
    public Widget BuildSelfWidget(Factory factory, XmlElement xWidget)
    {
        string strType = xWidget.LocalName;
        
        TypeDescriptor tdesc;
        if (!_mapTypes.TryGetValue(strType, out tdesc))
        {
            ErrorThrow<ArgumentException>($"Invalid type {strType}.");
            return null;
        }
        
        /*
         * First create the widget including all of the attributes.
         */
        Widget w = new Widget() { Type = strType };
        
        ApplyTemplate(w, tdesc);

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


    public Widget BuildText(Factory factory, XmlText xText)
    {
        return null;
    }
    

    public Widget BuildWidget(Factory factory, XmlElement xWidget)
    {
        /*
         * Create the widget on its own
         */
        Widget w = BuildSelfWidget(factory, xWidget);
        
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
                    Widget wChildElement = BuildWidget(factory, xnChild as XmlElement);
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
        
        return w;
    }
    
    
    /**
     * Build a meaningful display from the xml input.
     *
     * Syntax: everything below root.
     */
    public Widget Build(Factory factory)
    {
        XmlElement xWidget = _xDoc.GetElementsByTagName("Widget")[0] as XmlElement;
        if (null == xWidget)
        {
            ErrorThrow<ArgumentException>("No widget found.");
        }

        return BuildWidget(factory, xWidget);
    }
        
    
    public Parser(XmlDocument xDoc)
    {
        _xDoc = xDoc;
    }


    static public void Unit()
    {
        string xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<Widget>
    <Flex direction=""vertical"">
        <Text>Item 1</Text>
        <Text>Item 2</Text>
    </Flex>
</Widget>
";

        Factory f = new();
        XmlDocument xDoc = new XmlDocument();
        xDoc.LoadXml(xml);
        Parser p = new(xDoc);
        Widget w = p.Build(f);
    }
}