using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;

namespace builtin.tools.kanshu;

public class Properties
{
    public SortedDictionary<string, string> Value { get; init; }

    public string this[string key]
    {
        get => Value[key];
    }

    public Properties(SortedDictionary<string, string> props)
    {
        Value = new SortedDictionary<string, string>(props);
    }
}

