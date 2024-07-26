using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using static engine.Logger;

namespace engine;


/**
 * This is the one place tracking instantiated modules and their references.
 * While locally instantiated modules may be created by anyone, shared modules
 * need a single place to track.
 *
 * By nature, this is not a module, only a singleton registered by the engine.
 */
public sealed class ModuleFactory
{
    private object _lo = new();
    
    private readonly ConcurrentDictionary<Type, SemaphoreSlim> _keyLocks = new ConcurrentDictionary<Type, SemaphoreSlim>();
    
    private class Entry
    {
        public System.Type Type; 
        public int References;
        public IModule? Implementation;
    };

    private Dictionary<Type, Entry> _mapImplementations = new();


    public IModule FindModule<T>() => FindModule(typeof(T));

    public IModule FindModule(System.Type type)
    {
        Trace($"Trying to find module {type.ToString()}");
        var keyLock = _keyLocks.GetOrAdd(type, x => new SemaphoreSlim(1));
        keyLock.Wait();

        Entry entry;
        bool pleaseCreateInstance = false;
        lock (_lo)
        {
            if (_mapImplementations.TryGetValue(type, out entry))
            {
                ++entry.References;
            }
            else
            {
                entry = new Entry()
                {
                    Type = type,
                    Implementation = null,
                    References = 1
                };
                _mapImplementations[type] = entry;
                pleaseCreateInstance = true;
            }
        }

        try
        {
            if (pleaseCreateInstance)
            {
                object oModule = I.Instance.GetInstance(type);
                if (oModule == null)
                {
                    ErrorThrow<ArgumentException>($"Trying to pull invalid module of unknown type {type}.");
                }

                IModule? module = oModule as IModule;
                if (null == module)
                {
                    ErrorThrow<ArgumentException>($"Requested instance of type {type} is not a module.");
                }
                
                entry.Implementation = module;

                module.ModuleActivate();
            }
        }
        catch (Exception e)
        {
            /*
             * If there was a problem creating the module, remove the temporary entry
             */
            lock (_lo)
            {
                _mapImplementations.Remove(type);
            }

        }
        finally
        {
            keyLock.Release();
        }

        return entry.Implementation;
    }

    
    public void Unreference(IModule implementation)
    {
        // We just ignore unreferenced items today...        
    }
}