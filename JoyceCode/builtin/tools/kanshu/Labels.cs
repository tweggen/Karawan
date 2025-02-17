using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace builtin.tools.kanshu;

public class Labels
{
    [Flags]
    public enum AlterationFlags
    {
        ConsiderOld = 1,
        PriorizeNew = 2,
        BindValues = 4
    };


    public SortedDictionary<string, Value> Map { get; init; }

    public Value this[string key]
    {
        get => Map[key];
    }

    public Labels(SortedDictionary<string, Value> map)
    {
        Map = map;
    }


    static private void _mergeFrom(Scope scope, SortedDictionary<string, Value> to,
        SortedDictionary<string, Value> from, AlterationFlags flags)
    {
        foreach (var kvp in from)
        {
            Value v;
            if ((flags & AlterationFlags.BindValues) != 0)
            {
                v = kvp.Value.GetBoundValue(scope);
            }
            else
            {
                v = kvp.Value;
            }

            to.Add(kvp.Key, v);
        }
    }
    
    
    public static Labels FromMerge(Scope scope, Labels old, Labels template, AlterationFlags flags)
    {
        Labels l = new(new());

        if ((flags & AlterationFlags.PriorizeNew) != 0)
        {
            if ((flags & AlterationFlags.ConsiderOld) != 0)
            {
                _mergeFrom(scope, l.Map, old.Map, flags);
            }

            _mergeFrom(scope, l.Map, template.Map, flags);
        }
        else
        {
            _mergeFrom(scope, l.Map, template.Map, flags);
            
            if ((flags & AlterationFlags.ConsiderOld) != 0)
            {
                _mergeFrom(scope, l.Map, old.Map, flags);
            }
        }

        return l;
    }
    
    
    public static Labels FromStrings(SortedDictionary<string, string> map)
    {
        SortedDictionary<string, Value> mapValues = new();
        foreach (var kvp in map)
        {
            mapValues.Add(kvp.Key, new ConstantValue(kvp.Value));    
        }

        return new Labels(mapValues);
    }
    
    
    public static Labels FromDollars(SortedDictionary<string, string> map)
    {
        SortedDictionary<string, Value> mapValues = new();
        foreach (var kvp in map)
        {
            Value value;
            if (kvp.Value.Length > 1 && kvp.Value[0] == '$')
            {
                if (kvp.Value[1] != '$')
                {
                    value = new BoundValue(kvp.Value.Substring(1));
                }
                else
                {
                    value = new ConstantValue(kvp.Value.Substring(2));
                }
            }
            else
            {
                value = new ConstantValue(kvp.Value);
            }
            
            mapValues.Add(kvp.Key, value);    
        }

        return new Labels(mapValues);
    }

}

 