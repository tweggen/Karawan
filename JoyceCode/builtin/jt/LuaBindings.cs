using System;
using System.Runtime.InteropServices.JavaScript;
using engine.gongzuo;
using static engine.Logger;

namespace builtin.jt;

/**
 * Add all bindings to the lua language.
 */
public class LuaBindings
{
    private object _lo = new();
    
    public readonly Factory Factory;
    public readonly LuaScriptEntry ScriptEntry;

    private Parser? _parser = null;
    public Parser? Parser {
        get
        {
            lock (_lo)
            {
                return _parser;
            }
        }
        set
        {
            lock (_lo)
            {
                _parser = value;
            }
        }
    } 
    
    
    /**
     * open a certain menu.
     *
     * We need the parser from the calling widget to reference the given id.
     * We use the factory that is default to the bindings.
     */
    public void open(string id)
    {
        LuaWidgetContext? context;
        try
        {
            context = ScriptEntry.LuaState["_context"] as LuaWidgetContext;
            if (context == null)
            {
                ErrorThrow<InvalidOperationException>("Unable to find call context.");
                return;
            }

            var wNew = context._parser.Build(context._factory, id);
            context._factory.FindRootWidget().AddChild(wNew);
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
        LuaWidgetContext? context;
        try
        {
            context = ScriptEntry.LuaState["_context"] as LuaWidgetContext;
            if (context == null)
            {
                ErrorThrow<InvalidOperationException>("Unable to find call context.");
                return;
            }

            RootWidget wRoot = context._factory.FindRootWidget();
            var children = wRoot.Children;
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
        catch (Exception e)
        {
            Error($"Exception executing close call.");
            return;
        }
    }


    /**
     * 
     */
    public void replaceAll(string id)
    {
        closeAll();
        open(id);
    }

    
    public LuaBindings(LuaScriptEntry lse, Factory factory)
    {
        ScriptEntry = lse;
        Factory = factory;
    }
}