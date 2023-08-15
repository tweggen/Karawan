using System;
using System.Collections.Generic;

namespace engine;


public class ComponentInfo
{
    public Type Type;
    public string ValueAsString;
    public object Value;
}

public class EntityComponentTypeReader : DefaultEcs.Serialization.IComponentTypeReader
{
    public SortedDictionary<string, ComponentInfo> DictComponentTypes = new();
    private DefaultEcs.Entity _entity;

    public void OnRead<T>(int maxCapacity)
    {
        if (_entity.Has<T>())
        {
            Type type = typeof(T);
            string strType = typeof(T).ToString();
            string strValueRepresentation = "(value unprintable)";
            Type t = typeof(T);
            object value = null;
            try
            {
                strValueRepresentation = _entity.Get<T>().ToString();
                value = _entity.Get<T>();
            }
            catch (Exception ex)
            {

            }

            DictComponentTypes[strType] = new ComponentInfo()
            {
                Type = type, 
                ValueAsString = strValueRepresentation,
                Value = value
            };
        }
    }

    public EntityComponentTypeReader(DefaultEcs.Entity entity)
    {
        _entity = entity;
    }
}
