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

    public bool IgnoreDoubleActivation { get; set; } = false;
    
    
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
    
    protected void ActivateMyModule<T>() where T : class, IModule
    {
        IModule myModule = M<T>();
        if (myModule.IsModuleActive())
        {
            ErrorThrow<InvalidOperationException>($"My module of type {myModule.GetType()} already was active.");
        }
        myModule.ModuleActivate();
    }


    protected void DeactivateMyModule<T>() where T : class, IModule
    {
        IModule myModule = M<T>();
        if (!myModule.IsModuleActive())
        {
            ErrorThrow<InvalidOperationException>($"My module of type {myModule.GetType()} was not active.");
        }
        myModule.ModuleDeactivate();
    }


    public virtual void Dispose()
    {
        // Do not dispose the dependant modules.
    }


    public bool IsModuleActive() => _isActivated;
    

    public virtual void ModuleDeactivate()
    {
        List<IModule> activatedModules;
        lock (_lo)
        {
            if (!_isActivated)
            {
                Error($"Module was not activated.");
                return;
            }

            _isActivated = false;
            activatedModules = new List<IModule>(_activatedModules);
            _activatedModules.Clear();
        }
        
        foreach (var module in activatedModules)
        {
            if (module.IsModuleActive())
            {
                module.ModuleDeactivate();
            }
        }

        lock (_lo)
        {
            _mapModules.Clear();
            _engine = null;
        }
    }


    public virtual void ModuleActivate()
    {
        _engine = I.Get<Engine>();
        var deps = ModuleDepends();
        lock (_lo)
        {
            if (_isActivated)
            {
                if (IgnoreDoubleActivation)
                {
                    return;
                }
                else
                {
                    ErrorThrow<InvalidOperationException>($"Module ${this.GetType()} already activated.");
                }
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
                return;
            }

            lock (_lo)
            {
                _mapModules.Add(moduleDependency.ModuleType, module);
            }

            
            bool didActivate = moduleDependency.Activate();
            if (didActivate)
            {
                lock (_lo)
                {
                    _activatedModules.Add(module);
                }
            }
        }
    }

}