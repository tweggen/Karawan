using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace builtin.loader;

public class ModelProperties
{
    public SortedDictionary<string, string> Properties = new();

    public string this[string key]
    {
        get => Properties[key];
        set
        {
            Properties[key] = value;
        }
    }

    public override string ToString()
    {
        string toString = "ModelProperties: {";
        bool isFirst = true;
        foreach (var kvp in Properties)
        {
            if (isFirst)
            {
                isFirst = false;
            }
            else
            {
                toString += ", ";
            }
            toString += $"\"{kvp.Key}\": \"{kvp.Value}\"";
        }

        return toString;
    }
}
