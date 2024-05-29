using System;
using System.Xml;
using engine;
using static engine.Logger;

namespace builtin.jt;

public class Factory
{
    private builtin.jt.ImplementationFactory _implementationFactory = I.Get<builtin.jt.ImplementationFactory>();
        

    public Widget OpenOSD(string filename, string id)
    {
        try
        {
            /*
             * Read the xml
             */
            XmlDocument xDoc = new XmlDocument();
            xDoc.Load(engine.Assets.Open(filename));
            
            /*
             * Parse the document
             */
            var parser = new Parser(xDoc, _implementationFactory);
            
            /*
             * Open the default menu.
             */
            var wMenu = parser.Build(id);
            if (null != wMenu)
            {
                parser.Layer(wMenu["layer"].ToString()).AddChild(wMenu);
                parser.Layer(wMenu["layer"].ToString()).SetFocussedChild(wMenu.FindFirstFocussableChild());
            }

            return wMenu;
        }
        catch (Exception e)
        {
            Error($"Exception opening menu: {e}");
            throw e;
        }
    }
}