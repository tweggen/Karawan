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


    protected virtual IEnumerable<ModuleDependency> ModuleDepends() => new List<ModuleDependency>();
    
    
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

        _engine = null;
    }


    public virtual void ModuleActivate(engine.Engine engine)
    {
        _engine = engine;
        _moduleDependencies = ModuleDepends();
        foreach (var moduleDependency in _moduleDependencies)
        {
            try
            {
                Object implementation = I.Instance.GetInstance(moduleDependency.TypeName);
                moduleDependency.Implementation = implementation;
                if (moduleDependency.ActivateAsModule)
                {
                    var module = implementation as IModule;
                    if (module == null)
                    {
                        ErrorThrow($"Dependency {moduleDependency.TypeName} is not an IModule.", (m) => new InvalidCastException(m));
                    }
                        
                    module.ModuleActivate(engine);
                    _activatedModules.Add(module);
                }
            }
            catch (Exception e)
            {
                Error($"Unable to resolve dependency ${moduleDependency.TypeName}: {e}.");
            }
        }
    }

}