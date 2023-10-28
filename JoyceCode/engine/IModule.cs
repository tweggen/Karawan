using System;
using System.Collections.Generic;

namespace engine;

public class ModuleDependency
{
    private bool _isSettingTrue(string setting)
    {
        return engine.GlobalSettings.Get(setting) != "false";
    }
    
    public Func<bool> Condition;
    public Type ModuleType;
    public bool ActivateAsModule = true;
    public Object Implementation;

    public ModuleDependency(string setting, Type t)
    {
        Condition = () => _isSettingTrue(setting);
        ModuleType = t;
    }
    public ModuleDependency(Type t)
    {
        ModuleType = t;
    }
}


public interface IModule : IDisposable
{
    public IEnumerable<ModuleDependency> ModuleRequires();
    
    public void ModuleActivate(engine.Engine engine);
    public void ModuleDeactivate();
}