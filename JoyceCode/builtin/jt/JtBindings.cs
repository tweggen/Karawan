using System;
using System.Runtime.InteropServices.JavaScript;
using engine.gongzuo;
using static engine.Logger;

namespace builtin.jt;

/**
 * Add all bindings to the lua language.
 */
public class JtBindings
{
    private object _lo = new();

    public readonly Factory Factory;
    public readonly Parser Parser;

    
    /**
     * open a certain menu.
     *
     * We need the parser from the calling widget to reference the given id.
     * We use the factory that is default to the bindings.
     */
    public void open(string layer, string id)
    {
        try
        {
            var wNew = Parser.Build(id);
            Factory.Layer(layer).AddChild(wNew);
        }
        catch (Exception e)
        {
            Error($"Exception executing open call.");
            return;
        }
    }


    /**
     * Close all open widgets
     */
    public void closeAll(string layername)
    {
        try
        {
            RootWidget wRoot = Factory.Layer(layername);
            var children = wRoot.Children;
            if (null != children)
            {
                foreach (var child in children)
                {
                    try
                    {
                        child.Parent = null;
                    }
                    catch (Exception e)
                    {
                        Error($"Unable to remove widget from parent: {e}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Error($"Exception executing close call.");
            return;
        }
    }


    /**
     * Close all windows and open a new one. 
     */
    public void replaceAll(string layername, string id)
    {
        closeAll(layername);
        open(layername, id);
    }

    
    public JtBindings(Factory factory, Parser parser)
    {
        Factory = factory;
        Parser = parser;
    }
}