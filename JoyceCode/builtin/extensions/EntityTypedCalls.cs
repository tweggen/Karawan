using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace builtin.extensions;

static public class EntityTypedCalls
{
    private static object _lo = new();
    
    private static Lazy<MethodInfo> _setMethod = new(() => 
        typeof(DefaultEcs.Entity).GetMethods()
            .Where(m => m.Name == "Set" && m.GetParameters().Length == 1)
            .FirstOrDefault(m => true));
    
    private static Lazy<MethodInfo> _removeMethod = new(() => 
        typeof(DefaultEcs.Entity).GetMethods()
            .Where(m => m.Name == "Remove" && m.GetParameters().Length == 0)
            .FirstOrDefault(m => true));
    
    private static SortedDictionary<System.Type, MethodInfo> _removeCache = new();
    private static SortedDictionary<System.Type, MethodInfo> _setCache = new();
    
    public static void Set(this in DefaultEcs.Entity entity, System.Type type, in object comp)
    {
        /*
         * Find the type specific set method of entity.
         */
        MethodInfo genericMethod;
        lock (_lo)
        {
            if (!_setCache.TryGetValue(type, out genericMethod))
            {
                genericMethod = _setMethod.Value.MakeGenericMethod(type);
                _setCache.Add(type, genericMethod);
            }
        }

        /*
         * Set the deserialized component to the entity.
         */
        genericMethod.Invoke(entity, new object[] { comp });
    }


    public static void Remove(this in DefaultEcs.Entity entity, System.Type type)
    {
        /*
         * Find the type specific set method of entity.
         */
        MethodInfo genericMethod;
        lock (_lo)
        {
            if (!_removeCache.TryGetValue(type, out genericMethod))
            {
                genericMethod = _removeMethod.Value.MakeGenericMethod(type);
                _removeCache.Add(type, genericMethod);
            }
        }

        /*
         * Set the deserialized component to the entity.
         */
        genericMethod.Invoke(entity, new object[] {});
    }
}