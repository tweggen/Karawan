using System.Collections.Immutable;
using System.Xml;

namespace Joyce.builtin.jt;

public class Parser
{
    private XmlDocument _xDoc;


    public Widget BuildWidget(Factory factory, XmlElement xWidget)
    {
        /*
         * First create the widget including all of the attributes.
         */
        Widget w = new() { Factory = factory };
        int l = xWidget.Attributes.Count;
        for (int i=0; i<l; ++i)
        {
            var attr = xWidget.Attributes[i];
            w[attr.LocalName] = attr.Value;
        }
        
        /*
         * Then iterate through its children.
         */
    }
    
    
    /**
     * Build a meaningful display from the xml input.
     *
     * Syntax: everything below root.
     */
    public Widget Build(Factory factory)
    {
        XmlElement xRoot = _xDoc.GetElementsByTagName("root")[0] as XmlElement;

        return BuildWidget(factory, xRoot);
    }
        
    
    public Parser(XmlDocument xDoc)
    {
        _xDoc = xDoc;
    }
}