using System.Text.Json;
using System.Text.Json.Nodes;
using builtin.modules.inventory.components;
using engine;

namespace builtin.modules.inventory;

public class PickableDirectory : ObjectFactory<string, PickableDescription>, engine.ISerializable
{
    public void SaveTo(ref JsonObject jo)
    {
        throw new System.NotImplementedException();
    }

    public void SetupFrom(JsonElement je)
    {
        foreach (var jp in je.EnumerateObject())
        {
            var path = jp.Name;
            var jeV = jp.Value;
            
            
            PickableDescription pd = new()
            {
                Path = path,
                Name = jeV.GetProperty("name").GetString(),
                Description = jeV.TryGetProperty("description", out var jeDescription) ? jeDescription.GetString() : null,
                UseAction = jeV.TryGetProperty("useAction", out var jeUseAction) ? new GameAction(jeUseAction.GetString()) : null,
                Weight = jeV.TryGetProperty("weight", out var jeWeight) ? jeWeight.GetSingle() : 0f,
                Volume = jeV.TryGetProperty("volume", out var jeVolume) ? jeVolume.GetSingle() : 0f,
            };

            FindAdd(path, pd);
        }
    }
}