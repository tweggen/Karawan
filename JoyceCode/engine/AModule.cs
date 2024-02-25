using System;
using System.Collections.Generic;
using static engine.Logger;

namespace engine;

public abstract class AModule : IModule
{
    protected object _lo = new();
    private bool _isActivated;
    protected Engine _engine;
    protected IEnumerable<IModuleDependency> _moduleDependencies = null;
    
    private List<IModule> _activatedModules = new();
    private Dictionary<Type, IModule> _mapModules = new();

    
    public virtual IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>();

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


    public virtual void Dispose()
    {
        // Do not dispose the dependant modules.
    }
    

    public virtual void ModuleDeactivate()
    {
        lock (_lo)
        {
            if (!_isActivated)
            {
                ErrorThrow<InvalidOperationException>($"Module was not activated.");
            }

            _isActivated = false;
        }

        foreach (var module in _activatedModules)
        {
            module.ModuleDeactivate();
        }

        _mapModules.Clear();
        _activatedModules.Clear();
        _engine = null;
    }


    public virtual void ModuleActivate(engine.Engine engine0)
    {
        _engine = engine0;
        var deps = ModuleDepends();
        lock (_lo)
        {
            if (_isActivated)
            {
                ErrorThrow<InvalidOperationException>($"Module already activated.");
            }

            _isActivated = true;
            _moduleDependencies = deps;
        }

        foreach (var moduleDependency in _moduleDependencies)
        {
            bool condition = true;
            if (moduleDependency.Condition != null)
            {
                condition = moduleDependency.Condition();
            }

            if (!condition) continue;
            var module = moduleDependency.Implementation;
            if (module == null)
            {
                ErrorThrow($"Unable to instantiate moduleDependency.",
                    (m) => new InvalidOperationException(m));
            }

            _mapModules.Add(moduleDependency.ModuleType, module);
            if (moduleDependency.Activate)
            {
                /*
                 * Only activate the module if it hasn't been activated yet. 
                 */
                // TXWTODO: This basically is a workaround.
                if (!_engine.HasModule(module))
                {
                    module.ModuleActivate(_engine);
                }
                _activatedModules.Add(module);
            }
        }
    }

}