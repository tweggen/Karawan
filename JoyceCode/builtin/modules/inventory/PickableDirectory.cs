using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using builtin.modules.inventory.components;
using engine;

namespace builtin.modules.inventory;


/**
 * The directory of available pickables. Contains the descriptions.
 */
public class PickableDirectory : ObjectFactory<string, PickableDescription>, engine.ISerializable
{
    public void SaveTo(JsonObject jo)
    {
        throw new System.NotImplementedException();
    }

    public Func<Task> SetupFrom(JsonNode jn)
    {
        if (jn is JsonObject obj)
        {
            foreach (var kvp in obj)
            {
                var path = kvp.Key;
                var node = kvp.Value;

                PickableDescription pd = new()
                {
                    Path = path,
                    Name = node?["name"]?.GetValue<string>(),
                    Description = node?["description"]?.GetValue<string>(),
                    UseAction = node?["useAction"] is JsonNode useActionNode 
                        ? new GameAction(useActionNode.GetValue<string>()) 
                        : null,
                    Weight = node?["weight"]?.GetValue<float>() ?? 0f,
                    Volume = node?["volume"]?.GetValue<float>() ?? 0f,
                };

                FindAdd(path, pd);
            }
        }

        return () => Task.CompletedTask;
    }

}