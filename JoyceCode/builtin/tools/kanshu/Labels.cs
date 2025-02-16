using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;

namespace builtin.tools.kanshu;

public class Labels
{
    public SortedDictionary<string, string> Value { get; init; }

    public string this[string key]
    {
        get => Value[key];
    }

    public Labels(SortedDictionary<string, string> props)
    {
        Value = new SortedDictionary<string, string>(props);
    }
}

 