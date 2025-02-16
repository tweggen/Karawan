using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;

namespace builtin.tools.kanshu;

public class Labels
{
    public SortedDictionary<string, Value> Map { get; init; }

    public Value this[string key]
    {
        get => Map[key];
    }

    public Labels(SortedDictionary<string, Value> map)
    {
        Map = new SortedDictionary<string, Value>(map);
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
}

 