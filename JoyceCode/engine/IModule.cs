using System;
using System.Collections.Generic;
using engine.news;
using static engine.Logger;

namespace engine;

public interface IModuleDependency
{
    public Type ModuleType { get; set; }
    public Func<bool> Condition { get; }
    public IModule Implementation { get; }
    
    public bool Activate();
    public void Deactivate();
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

    public bool ShallActivate { get; set; } = true;

    public abstract bool Activate();
    public abstract void Deactivate();
}

public class MyModule<T> : AModuleDependency where T:class  
{
    private IModule _implementation = null;

    public override void Deactivate()
    {
        if (ShallActivate)
        {
            _implementation?.ModuleDeactivate();
        }
    }


    public override bool Activate()
    {
        bool activated = false;
        if (ShallActivate)
        {
            _implementation?.ModuleActivate();
            activated = true;

        }
        return activated;
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
    private Lazy<IModule>? _implementation;
    
    public override void Deactivate()
    {
        if (null != _implementation)
        {
            if (ModuleType == typeof(InputEventPipeline))
            {
                int a = 1;
            }
            I.Get<ModuleFactory>().Unreference(_implementation.Value);
            _implementation = null;
        }
        else
        {
            ErrorThrow<InvalidOperationException>($"Tried to deactivate a module of type {ModuleType} twice.");
        }
    }


    public override bool Activate()
    {
        // No need to explicitely reference, we do that on first access time.
        return false;
    }

    
    public override IModule Implementation
    {
        get => _implementation.Value; 
    }

    public SharedModule()
    {
        ModuleType = typeof(T);
        _implementation = new(() => I.Get<ModuleFactory>().FindModule(ModuleType));
    }
}



public interface IModule : IDisposable
{
    public IEnumerable<IModuleDependency> ModuleDepends();

    public bool IsModuleActive();
    public void ModuleActivate();
    public void ModuleDeactivate();
}