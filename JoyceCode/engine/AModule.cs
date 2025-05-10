using System;
using System.Collections.Generic;
using static engine.Logger;

namespace engine;


public class ModuleTracker : IDisposable
{
    private object _lo = new();
    
    private List<IModule> _activatedModules = new();
    private Dictionary<Type, IModule> _mapModules = new();
    public bool IgnoreDoubleActivation { get; set; } = false;

    public IEnumerable<IModuleDependency> ModuleDependencies = null;
   

    required public IModule Module;
    public bool _isActivated = false;

    
    public T M<T>() where T : class
    {
        if (_mapModules.TryGetValue(typeof(T), out var mod))
        {
            return mod as T;
        }
        else
        {
            Error($"Unable to find module {typeof(T)}");
            return null;
        }
    }

    
    public bool IsMyModuleActive<T>() where T : class, IModule
    {
        IModule myModule = M<T>();
        return myModule.IsModuleActive();
    }
    
    
    public void ActivateMyModule<T>() where T : class, IModule
    {
        IModule myModule = M<T>();
        if (myModule.IsModuleActive())
        {
            ErrorThrow<InvalidOperationException>($"My module of type {myModule.GetType()} already was active.");
        }
        myModule.ModuleActivate();
    }


    public void DeactivateMyModule<T>() where T : class, IModule
    {
        IModule myModule = M<T>();
        if (!myModule.IsModuleActive())
        {
            ErrorThrow<InvalidOperationException>($"My module of type {myModule.GetType()} was not active.");
        }
        myModule.ModuleDeactivate();
    }

    
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
        }
    }


    public virtual void ModuleActivate()
    {
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
                    ErrorThrow<InvalidOperationException>($"Module {Module} already activated.");
                }
            }

            _isActivated = true;
        }

        foreach (var moduleDependency in ModuleDependencies)
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
    

    public void Dispose()
    {
        // Nothing to dispose
    }
}


public abstract class AModule : IModule
{
    protected ModuleTracker _moduleTracker;
    
    protected object _lo = new();
    
    protected Engine _engine;

    public virtual IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>();

    
    protected T M<T>() where T : class => _moduleTracker.M<T>();
    protected void ActivateMyModule<T>() where T : class, IModule => _moduleTracker.ActivateMyModule<T>();
    protected void DeactivateMyModule<T>() where T : class, IModule => _moduleTracker.DeactivateMyModule<T>();
    protected bool IsMyModuleActive<T>() where T : class, IModule => _moduleTracker.IsMyModuleActive<T>();

    
    protected virtual void OnModuleDeactivate()
    {
    }

    protected virtual void OnModuleActivate()
    {
    }

    protected virtual void _internalOnModuleDeactivate()
    {
    }

    protected virtual void _internalOnModuleActivate()
    {
    }
    

    public void ModuleDeactivate()
    {
        try
        {
            OnModuleDeactivate();
        }
        catch (Exception e)
        {
            Warning($"Exception while executing Deactivate call for module: {e}.");
        }
        _internalOnModuleDeactivate();

        _engine.RemoveModule(this);
        _moduleTracker.ModuleDeactivate();   
    }

    
    
    public void ModuleActivate()
    {
        _engine = I.Get<Engine>();
        
        /*
         * Generate the dependencies on demand. This is not possible at construction time.
         */
        _moduleTracker.ModuleDependencies = ModuleDepends();
        _moduleTracker.ModuleActivate();
        _engine.AddModule(this);

        _internalOnModuleActivate();
        try
        {
            OnModuleActivate();
        }
        catch (Exception e)
        {
            Warning($"Exception while executing Activate call for module: {e}.");
        }
    } 
    
    
    public virtual bool IsModuleActive() => _moduleTracker._isActivated;

    public virtual void Dispose()
    {
        _moduleTracker.Dispose();
    }


    protected AModule()
    {
        _moduleTracker = new() { Module = this };
    }
}