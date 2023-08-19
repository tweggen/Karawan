using System;
using System.Collections.Generic;
using static engine.Logger;

namespace engine;

public abstract class AModule : IModule
{
    protected List<string> _moduleRequires = new();
    private List<IModule> _activatedModules = new();
    
    public IEnumerable<string> ModuleRequires()
    {
        return new List<string>(_moduleRequires);
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
        foreach (var moduleTypeString in _moduleRequires)
        {
            try
            {
                IModule module = Implementations.Instance.GetInstance(moduleTypeString) as IModule;
                if (module == null)
                {
                    ErrorThrow("Depending implementation is not a module.", (m) => new InvalidCastException(m));
                }
                module.ModuleActivate(engine);
                _activatedModules.Add(module);
            }
            catch (Exception e)
            {
                Error($"Unable to resolve dependency ${moduleTypeString}: {e}.");
            }
        }
    }

}