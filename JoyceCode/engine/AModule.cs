using System;
using System.Collections.Generic;
using static engine.Logger;

namespace engine;

public abstract class AModule : IModule
{
    protected List<ModuleDependency> _moduleDependencies = new();
    private List<IModule> _activatedModules = new();
    
    public IEnumerable<ModuleDependency> ModuleRequires()
    {
        return new List<ModuleDependency>(_moduleDependencies);
    }


    public void Dispose()
    {
        // Do not dispose the dependant modules.
    }
    

    public void ModuleDeactivate()
    {
        foreach (var module in _activatedModules)
        {
            module.ModuleDeactivate();
        }
    }


    public void ModuleActivate(engine.Engine engine)
    {
        foreach (var moduleDependency in _moduleDependencies)
        {
            try
            {
                Object implementation = Implementations.Instance.GetInstance(moduleDependency.TypeName);
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