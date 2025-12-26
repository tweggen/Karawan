using System;
using System.Collections.Generic; 
using System.Text.Json.Serialization;
using static engine.Logger;
using static builtin.extensions.EntityTypedCalls;

namespace builtin.npc;


/**
 * A json driven state implementation.
 * This implementation reads data for components from json.
 * It would eventually set/remove them to the entity.
 */
public class JsonState : IStatePart
{
    [JsonInclude] public SortedDictionary<string, object> OnEnterComponents;
    [JsonInclude] public SortedSet<string> OnEnterRemove;
    
    [JsonInclude] public SortedSet<string> OnExitRemove;


    private void _removeAll(in DefaultEcs.Entity entity, in SortedSet<string> set)
    {
        foreach (var strType in set)
        {
            Type? type = Type.GetType(strType);
            if (null == type)
            {
                Warning($"Unable to resolve type {strType} for removal");
                return;
            }

            entity.Remove(type);
        }
    }
    
    
    private void _addAll(in DefaultEcs.Entity entity, in SortedDictionary<string, object> map)
    {
        foreach (var kvp in map)
        {
            Type? type = Type.GetType(kvp.Key);
            if (null == type)
            {
                Warning($"Unable to resolve type {kvp.Key} for removal");
                return;
            }

            entity.Set(type, kvp.Value);
        }
    }
    
    
    public void OnExit(in DefaultEcs.Entity entity)
    {
        _removeAll(entity, OnExitRemove);
    }
    
    
    public void OnEnter(in DefaultEcs.Entity entity)
    {
        _removeAll(entity, OnEnterRemove);
        
        _addAll(entity, OnEnterComponents);
    }
}