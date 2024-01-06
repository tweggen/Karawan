using System.Collections.Generic;

namespace engine;


/**
 * Keep a map of Objects by id.
 */
public class EntityMap<ObjType> where ObjType : class, new()
{
    private object _lo = new();
    private Dictionary<int, ObjType> _idMap = new();

    public ObjType Find(int id)
    {
        lock (_lo)
        {
            if (_idMap.TryGetValue(id, out var obj))
            {
                return obj;
            }

            obj = new ObjType();
            _idMap.Add(id, obj);
            return obj;
        }
    }
}