using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using builtin.tools;
using static engine.Logger;

namespace builtin.extensions;

static public class JsonObjectNumerics
{
    static public JsonObject From(in Vector3 v3) => new JsonObject
    {
        ["x"] = v3.X, ["y"] = v3.Y, ["z"] = v3.Z
    };
    
    
    static public JsonObject From(in Vector2 v2) => new JsonObject
    {
        ["x"] = v2.X, ["y"] = v2.Y
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
    
    
    static public Vector3 ToVector3(in JsonElement je)
    {
        return new Vector3(
            je.GetProperty("x").GetSingle(),
            je.GetProperty("y").GetSingle(),
            je.GetProperty("z").GetSingle()
        );
    }
    
    
    static public Vector2 ToVector2(in JsonNode jn)
    {
        var jo = jn.AsObject();
        return new Vector2((float)jo["x"], (float)jo["y"]);
    }
    
    
    static public Vector2 ToVector2(in JsonElement je)
    {
        return new Vector2(
            je.GetProperty("x").GetSingle(),
            je.GetProperty("y").GetSingle()
            );
    }
    
    
    static public Quaternion ToQuaternion(in JsonNode jn)
    {
        var jo = jn.AsObject();
        return new Quaternion((float)jo["x"], (float)jo["y"], (float)jo["z"], (float)jo["w"]);
    }
    
    
    static public Quaternion ToQuaternion(in JsonElement je)
    {
        return new Quaternion(
            je.GetProperty("x").GetSingle(),
            je.GetProperty("y").GetSingle(),
            je.GetProperty("z").GetSingle(),
            je.GetProperty("w").GetSingle()
        );
    }
    
    
    static public List<Vector3> ToVector3List(in JsonNode jn)
    {
        var ienu = jn.AsArray().Select((jnVector) =>
        {
            // Trace($"{jnVector}");
            var v3 = ToVector3(jnVector);
            // Trace($"{v3}");
            return v3;
        });
        return new List<Vector3>(ienu);
    }

    static public Vector3 AnyOf(in RandomSource rnd, in List<Vector3> list)
    {
        int l = list.Count;
        if (0 == l) return Vector3.Zero;
        return list[((int)(rnd.GetFloat() * l)) % l];
    }

}