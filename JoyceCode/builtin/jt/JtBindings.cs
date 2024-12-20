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
            Factory.OpenOSD(Parser, id);
        }
        catch (Exception e)
        {
            Error($"Exception executing open call: {e}.");
            return;
        }
    }


    public void close(string layername, string id)
    {
        try
        {
            Factory.CloseOSD(layername, id);
        }
        catch (Exception e)
        {
            Error($"Exception executing close call: {e}.");
        }
    }
    
    
    /**
     * Close all open widgets
     */
    public void closeAll(string layername)
    {
        Factory.CloseAll(layername);
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