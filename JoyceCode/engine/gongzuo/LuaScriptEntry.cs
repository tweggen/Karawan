using System;
using System.Collections.Generic;
using System.Text;
using NLua;
using static engine.Logger;

namespace engine.gongzuo;

public class LuaScriptEntry : IDisposable
{
    private string _luaScript;

    public string LuaScript
    {
        get => _luaScript;
        set => _setLuaScript(value);
    }

    private int _setScriptVersion = 0;
    private int _compiledScriptVersion = 0;
    private Lua _luaState;

    public Lua LuaState
    {
        get => _luaState;
    }

    private LuaFunction _luaFunction;

    private List<LuaBindingFrame>? _luaBindings = null;

    
    /**
     * Assign a lua script to this script entry
     */
    private void _setLuaScript(string luaScript)
    {
        if (luaScript == _luaScript) return;
        _luaScript = luaScript;
        ++_setScriptVersion;
    }


    private int _doCompile()
    {
        if (null == _luaScript)
        {
            Error($"No script setup.");
            return -1;
        }

        try
        {
            _luaFunction = _luaState.LoadString(_luaScript, "component lua script");
            _compiledScriptVersion = _setScriptVersion;
            return 0;
        }
        catch (Exception e)
        {
            Error($"Exception parsing script: {e}.");
            return -1;
        }
    }


    private void _ensureCompiled()
    {
        if (_setScriptVersion == _compiledScriptVersion) return;

        Shutdown();
        try
        {
            Initialize();
            _doCompile();

        }
        catch (Exception e)
        {
            Error($"Exception while loading lua script: {e}");
        }
        if (null == _luaFunction)
        {
            ErrorThrow<InvalidOperationException>($"No lua function compiled.");
        }
    }


    /**
     * Apply a single frame of bindings to the luastate.
     */
    private void _applyBindingFrame(LuaBindingFrame? lbf)
    {
        if (lbf != null && lbf.MapBindings != null)
        {
            foreach (var kvp in lbf.MapBindings)
            {
                _luaState[kvp.Key] = kvp.Value;
            }
        }

    }

    
    /**
     * Apply the previously stored bindings to the newly created lua state.
     */
    private void _applyBindings()
    {
        if (_luaBindings != null)
        {
            foreach (var lbf in _luaBindings)
            {
                _applyBindingFrame(lbf);
            }
        }
    }


    /**
     * Apply the given binding frame (containing a map of bindings)
     * to the current script entry.
     */
    public void PushBinding(LuaBindingFrame lbf)
    {
        if (null == _luaState)
        {
            if (null == _luaBindings)
            {
                _luaBindings = new();
            }

            _luaBindings.Add(lbf);
        }
        else
        {
            _applyBindingFrame(lbf);
        }
    }


    public void Shutdown()
    {
        if (null != _luaState)
        {
            _luaState.Dispose();
            _luaState = null;
        }
    }


    public void Initialize()
    {
        _luaState = new Lua();
        _luaState.State.Encoding = Encoding.UTF8;
        _applyBindings();
    }


    public void Dispose()
    {
        Shutdown();
    }


    private object[] _emptyResult = new object[0];

    
    /**
     * Call the function represented by this script entry.
     */
    public object[] Call()
    {
        _ensureCompiled();

        return _luaFunction.Call();
    }


    /**
     * Call the function represented by this script entry, taking the
     * first result as the only result.
     */
    public object CallSingleResult()
    {
        _ensureCompiled();
        
        object[] results = _luaFunction.Call();
        if (results != null && results.Length >= 1)
        {
            return results[0];
        }
        return null;
    }
    

    /**
     * Call the function represented by this script entry, taking the
     * first result as the only result, interpreting it as a string.
     */
    public object CallStringResult()
    {
        _ensureCompiled();

        object result = CallSingleResult();
        if (result==_emptyResult)
        {
            result = "";
        }

        return result;
    }
}