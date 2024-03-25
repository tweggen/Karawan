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
    
    public readonly Parser Parser;

    /**
     * open a certain menu.
     *
     * We need the parser from the calling widget to reference the given id.
     * We use the factory that is default to the bindings.
     */
    public void open(string id)
    {
        try
        {
            var wNew = Parser.Build(id);
            Parser.RootWidget.AddChild(wNew);
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
    public void closeAll()
    {
        try
        {
            RootWidget wRoot = Parser.RootWidget;
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
    public void replaceAll(string id)
    {
        closeAll();
        open(id);
    }

    
    public JtBindings(Parser parser)
    {
        Parser = parser;
    }
}