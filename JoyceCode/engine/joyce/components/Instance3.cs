
using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace engine.joyce.components;

[engine.IsPersistable]
public struct Instance3
{
    public InstanceDesc InstanceDesc { get; set; }

    #if false
    public Func<Task>? SetupFrom(JsonElement je) => InstanceDesc.SetupFrom(je.GetProperty("instanceDesc"));

    public void SaveTo(JsonObject jo)
    {
        JsonObject joInstance3 = new();
        JsonObject joInstanceDesc = new();
        InstanceDesc.SaveTo(joInstanceDesc);
        joInstance3.Add("instanceDesc", joInstanceDesc);
        jo.Add("instance3", joInstance3);
    }
    #endif

    public override string ToString()
    {
        return $"{{ instanceDesc: {InstanceDesc.ToString()} }}";
    }

    /**
     * Construct a new instance3.
     * Caution: This uses the lists from the description.
     */
    public Instance3(in engine.joyce.InstanceDesc instanceDesc)
    {
        InstanceDesc = instanceDesc;
    }
}
