using System;
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

            var luaFunction = _luaState.LoadString(luaScript, "component lua script");
            luaFunction.Call();
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
        
    }

    public void Dispose()
    {
        Shutdown();
    }


    public void Call(string script, string scriptName)
    {
        _luaState.DoString(script, scriptName);
    }
}