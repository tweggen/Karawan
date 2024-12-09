using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace engine.gongzuo;

public class API
{
    private SortedDictionary<string, object> _mapTemplateBindings = new();
    private LuaBindingFrame _luaBindingFrame;
    private JoyceBindings _joyceBindings;

    public void AddDefaultBinding(string key, Func<object> bindingFactory)
    {
        if (!_mapTemplateBindings.ContainsKey(key))
        {
            _mapTemplateBindings[key] = bindingFactory();
            _luaBindingFrame = null;
        }
    }


    public void RemoveDefaultBinding(string key, object bindings)
    {
        _mapTemplateBindings.Remove(key);
        _luaBindingFrame = null;
    }


    private void _createFrame()
    {
        if (null == _luaBindingFrame)
        {
            _luaBindingFrame = new LuaBindingFrame()
            {
                MapBindings = _mapTemplateBindings.ToFrozenDictionary()
            };

        }
    }


    public void PushBindings(LuaScriptEntry lse)
    {
        _createFrame();
        
        lse.PushBinding(_luaBindingFrame);
    }


    public API()
    {
        _joyceBindings = new JoyceBindings();
        _mapTemplateBindings["joyce"] = _joyceBindings;
    }
}


