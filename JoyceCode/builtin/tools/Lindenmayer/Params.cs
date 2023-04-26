using System.Collections.Generic;

namespace builtin.tools.Lindenmayer;

public class Params
{
    public SortedDictionary<string, float> Map;

    public Params Clone()
    {
        if (null != Map)
        {
            return new Params(new SortedDictionary<string, float>(Map));
        }
        else
        {
            return new Params(null);
        }
    }

    public float this[string key]
    {
        get => Map[key];
    }


    public Params(SortedDictionary<string, float> map)
    {
        Map = map;
    }
}