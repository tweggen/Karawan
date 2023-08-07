using System;
using System.Collections.Generic;
using static engine.Logger;

namespace engine.joyce;

internal class MaterialEntry
{
    public object Lock;
    public string Name;
    public Func<string, Material>? FactoryFunction;
    public Material Instance;
}


public class MaterialCache
{
    private static readonly MaterialCache _singleton = new MaterialCache();
    
    private object _lo = new();
    private SortedDictionary<string, MaterialEntry> _mapMaterials = new();


    public Material FindMaterial(in Material referenceMaterial)
    {
        string key = referenceMaterial.ToString();
        lock (_lo)
        {
            if (_mapMaterials.TryGetValue(key, out var me))
            {
                return me.Instance;
            }
            MaterialEntry instanceEntry = new()
            {
                Lock = new(),
                Name = referenceMaterial.Name,
                FactoryFunction = null,
                Instance = referenceMaterial
            };
            _mapMaterials[key] = instanceEntry;
            return referenceMaterial;
        }
    }
    
    
    public void RegisterFactory(string name, Func<string, Material> factory)
    {
        lock (_lo)
        {
            if (_mapMaterials.TryGetValue(name, out _))
            {
                return;
            }
            MaterialEntry instanceEntry = new()
            {
                Lock = new(),
                Name = name,
                FactoryFunction = factory,
                Instance = null
            };

            _mapMaterials[name] = instanceEntry;
        }
    }


    public static void Register(string name, Func<string, Material> factory)
    {
        Instance.RegisterFactory(name, factory);
    }


    public Material GetInstance(string name)
    {
        MaterialEntry instanceEntry;
        lock (_lo)
        {
            if (!_mapMaterials.TryGetValue(name, out instanceEntry))
            {
                ErrorThrow($"Requested unknown material {name}.", (m)=>new ArgumentException(m));
            }

            if (null == instanceEntry)
            {
                ErrorThrow($"Material entry for type {name} was null.", (m)=>new InvalidOperationException(m));
            }
        }

        lock (instanceEntry.Lock)
        {
            if (null != instanceEntry.Instance)
            {
                return instanceEntry.Instance;
            }

            if (null == instanceEntry.FactoryFunction)
            {
                ErrorThrow($"No factory found for type {name}", (m) => new InvalidOperationException(m));
            }

            instanceEntry.Instance = instanceEntry.FactoryFunction(name);

            return instanceEntry.Instance;
        }


    }


    public static Material Get(string name)
    {
        return Instance.GetInstance(name);
    }



    private MaterialCache()
    {
    }
    
    
    public static MaterialCache Instance
    {
        get
        {
            return _singleton;
        }
    }

}