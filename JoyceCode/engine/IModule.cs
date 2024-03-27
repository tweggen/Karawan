using System;
using System.Collections.Generic;
using static engine.Logger;

namespace engine;

public interface IModuleDependency
{
    public Type ModuleType { get; set; }
    public Func<bool> Condition { get; }
    public IModule Implementation { get; }
    public bool Activate { get; }
    public bool Deactivate { get; }
}


public abstract class AModuleDependency : IModuleDependency
{
    protected bool _isSettingTrue(string setting)
    {
        return engine.GlobalSettings.Get(setting) != "false";
    }
    
    public Type ModuleType { get; set; }
    public Func<bool> Condition { get; protected set; }
    public abstract IModule Implementation { get; }
    public bool Activate { get; set; } = true;
    public abstract bool Deactivate { get;  }
}

public class MyModule<T> : AModuleDependency where T:class  
{
    private IModule _implementation = null;
    
    public override bool Deactivate
    {
        get => true; 
    }

    public override IModule Implementation
    {
        get
        {
            if (null == _implementation)
            {
                try
                {
                    Type newType = ModuleType;
                    Object implementation = Activator.CreateInstance(newType);
                    var module = implementation as IModule;
                    if (module == null)
                    {
                        ErrorThrow($"Dependency {ModuleType.FullName} is not an IModule.",
                            (m) => new InvalidCastException(m));
                    }

                    _implementation = module;
                }
                catch (Exception e)
                {
                    Error($"Unable to resolve dependency {ModuleType.FullName} : {e}.");
                }
            }
            return _implementation;
        }
    }

    public MyModule(string setting)
    {
        Condition = () => _isSettingTrue(setting);
        ModuleType = typeof(T);
    }
    public MyModule()
    {
        ModuleType = typeof(T);
    }
}

public class SharedModule<T> : AModuleDependency where T: class
{
    public override bool Deactivate
    {
        get => false; 
    }

    public override IModule Implementation
    {
        get => I.Instance.GetInstance(ModuleType) as IModule;
    }

    public SharedModule()
    {
        ModuleType = typeof(T);
    }
}



public interface IModule : IDisposable
{
    public IEnumerable<IModuleDependency> ModuleDepends();
    
    public void ModuleActivate();
    public void ModuleDeactivate();
}