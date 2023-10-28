using System;
using System.Collections.Generic;
using static engine.Logger;

namespace engine;

public abstract class AModule : IModule
{
    protected object _lo = new();
    protected Engine _engine;
    protected IEnumerable<ModuleDependency> _moduleDependencies = null;
    
    private List<IModule> _activatedModules = new();
    private Dictionary<Type, IModule> _mapModules = new();

    
    protected virtual IEnumerable<ModuleDependency> ModuleDepends() => new List<ModuleDependency>();

    protected T M<T>() where T : class
    {
        if (_mapModules.TryGetValue(typeof(T), out var mod))
        {
            return mod as T;
        }
        else
        {
            return null;
        }
    }
    
    
    public virtual IEnumerable<ModuleDependency> ModuleRequires()
    {
        return new List<ModuleDependency>(_moduleDependencies);
    }


    public virtual void Dispose()
    {
        // Do not dispose the dependant modules.
    }
    

    public virtual void ModuleDeactivate()
    {
        foreach (var module in _activatedModules)
        {
            module.ModuleDeactivate();
        }

        _mapModules.Clear();
        _activatedModules.Clear();
        _engine = null;
    }


    public virtual void ModuleActivate(engine.Engine engine)
    {
        _engine = engine;
        _moduleDependencies = ModuleDepends();
        foreach (var moduleDependency in _moduleDependencies)
        {
            bool condition = true;
            if (moduleDependency.Condition != null)
            {
                condition = moduleDependency.Condition();
            }

            if (!condition) continue;
            
            try
            {
                Type newType = moduleDependency.ModuleType;
                Object implementation = Activator.CreateInstance(newType);
                moduleDependency.Implementation = implementation;

                var module = implementation as IModule;
                if (module == null)
                {
                    ErrorThrow($"Dependency {moduleDependency.ModuleType.FullName} is not an IModule.",
                        (m) => new InvalidCastException(m));
                }
                _mapModules.Add(moduleDependency.ModuleType, module);
                if (moduleDependency.ActivateAsModule)
                {

                    module.ModuleActivate(engine);
                    _activatedModules.Add(module);
                }
            }
            catch (Exception e)
            {
                Error($"Unable to resolve dependency {moduleDependency.ModuleType.FullName} : {e}.");
            }
        }
    }

}