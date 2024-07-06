using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace builtin.tools.Lindenmayer;

public class Params
{
    public JsonObject Map;

    public Params Clone()
    {
        return new Params(Map.DeepClone() as JsonObject);
    }

    public JsonNode this[string key]
    {
        get => Map[key];
    }


    public Params(JsonObject map)
    {
        Map = map;
    }
}