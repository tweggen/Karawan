using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using BepuPhysics;

namespace engine.physics.actions;

public abstract class ABase
{
    protected virtual void SelfToJson(JsonNode jn)
    {
        // Nothing proprietary to serialize.
    }

    public JsonNode ToJson(Log plog)
    {
        JsonNode jn = JsonSerializer.SerializeToNode(
            this, GetType(), plog.JsonSerializerOptions);
        jn["type"] = GetType().ToString();
        SelfToJson(jn);
        return jn;
    }

    public abstract int Execute(Log? plog, Simulation simulation);
}