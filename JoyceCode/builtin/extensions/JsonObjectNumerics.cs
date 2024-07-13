using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json.Nodes;
using static engine.Logger;

namespace builtin.extensions;

static public class JsonObjectNumerics
{
    static public JsonObject From(in Vector3 v3) => new JsonObject
    {
        ["x"] = v3.X, ["y"] = v3.Y, ["z"] = v3.Z
    };
    
    
    static public JsonObject From(in Quaternion q) => new JsonObject
    {
        ["x"] = q.X, ["y"] = q.Y, ["z"] = q.Z, ["w"] = q.W
    };


    static public JsonArray From(in IEnumerable<Vector3> listV3)
    {
        JsonNode[] nodes = new JsonNode[listV3.Count()];
        int idx = 0;
        foreach (var v3 in listV3) nodes[idx++] = From(v3);
        return new JsonArray(nodes);
    }


    static public Vector3 ToVector3(in JsonNode jn)
    {
        var jo = jn.AsObject();
        return new Vector3((float)jo["x"], (float)jo["y"], (float)jo["z"]);
    }
    
    
    static public Quaternion ToQuaternion(in JsonNode jn)
    {
        var jo = jn.AsObject();
        return new Quaternion((float)jo["x"], (float)jo["y"], (float)jo["z"], (float)jo["w"]);
    }
    
    
    static public IEnumerable<Vector3> ToVector3List(in JsonNode jn)
    {
        var ienu = jn.AsArray().Select((jnVector) =>
        {
            // Trace($"{jnVector}");
            var v3 = ToVector3(jnVector);
            Trace($"{v3}");
            return v3;
        });
        return ienu;
    }

}