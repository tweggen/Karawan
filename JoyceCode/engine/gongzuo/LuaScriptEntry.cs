using System;
using System.Collections.Generic;
using System.Text;
using NLua;
using static engine.Logger;

namespace engine.gongzuo;

public class LuaScriptEntry : IDisposable
{
    private string _luaScript;
    public string LuaScript { 
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

    private void _setLuaScript(string luaScript)
    {
        if (luaScript == _luaScript && _setScriptVersion == _compiledScriptVersion) return;
        Shutdown();
        int res = -1;
        try
        {
            Initialize();
            _compile(luaScript);
        }
        catch (Exception e)
        {
            Error($"Exception while loading lua script: {e}");
        }
    }


    private int _compile(string luaScript)
    {
        if (null == luaScript)
        {
            return -1;
        }
        try
        {
            // TXWTODO: Add prebound script content, kind of global declarations

            _luaFunction = _luaState.LoadString(luaScript, "component lua script");
            _luaScript = luaScript;
            ++_setScriptVersion;
            _compiledScriptVersion = _setScriptVersion;
            return 0;
        }
        catch (Exception e)
        {
            Error($"Exception parsing script: {e}.");
            return -1;
        }
    }


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


    private void _applyBindings()
    {
        foreach (var lbf in _luaBindings)
        {
            _applyBindingFrame(lbf);
        }
    }
    
    
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


    public void Call()
    {
        if (null == _luaFunction)
        {
            ErrorThrow<InvalidOperationException>($"No lua function loaded.");
            return;
        }

        _luaFunction.Call();
    }
}