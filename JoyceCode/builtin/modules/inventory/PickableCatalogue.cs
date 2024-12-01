using System.Text.Json;
using System.Text.Json.Nodes;
using engine;

namespace builtin.modules.inventory;

public class PickableDirectory : ObjectFactory<string, PickableDescription>, engine.ISerializable
{
    public void SaveTo(ref JsonObject jo)
    {
        throw new System.NotImplementedException();
    }

    public void SetupFrom(JsonElement jo)
    {
        foreach (var jn in jo.EnumerateObject())
        {
            
        }
    }
}