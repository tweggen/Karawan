using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace builtin.loader;

public class ModelProperties
{
    public SortedDictionary<string, string> Properties { get; set; }

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
        string toString = "{";
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

        toString += '}';

        return toString;
    }
    

    public ModelProperties()
    {
        Properties = new();
    }


    public ModelProperties(ModelProperties? o)
    {
        Properties = o != null ? new(o.Properties) : new();
    }
}
