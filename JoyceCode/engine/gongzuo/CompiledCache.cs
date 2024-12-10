using System;
using System.Collections.Generic;

namespace engine.gongzuo;

public class CompiledCache
{
    private object _lo = new();
    
    /**
     * Contains anything we compile the properties to after user acesses it.
     */
    private Dictionary<object, IDisposable> _compiledProperties = new();


    public LuaScriptEntry Find(string evType, string script, Action<LuaScriptEntry> fLseSetup)
    {
        LuaScriptEntry? lse = null;
        lock (_lo)
        {
            if (_compiledProperties.TryGetValue(evType, out var oCompiled))
            {
                lse = oCompiled as LuaScriptEntry;
            }
            else
            {
                lse = new LuaScriptEntry();
                lse.LuaScript = script;
                
                /*
                 * Push the bindings for a call context from this widget on the bindings stack.
                 */
                fLseSetup(lse);

                _compiledProperties[evType] = lse;
            }
        }

        return lse;
    }
    

    public void Store(string key, IDisposable oCompiled)
    {
        lock (_lo)
        {
            _compiledProperties[key] = oCompiled;
        }
    }


    public void Invalidate(string key, out IDisposable? oldCompiled)
    {
        lock (_lo)
        {
            if (_compiledProperties.TryGetValue(key, out oldCompiled))
            {
                _compiledProperties.Remove(key);
            }
            else
            {
                oldCompiled = null;
            }
        }
    }
}