using System.Text.Json;
using System.Text.Json.Nodes;
using builtin;
using engine;
using engine.tale;
using static engine.Logger;

namespace nogame.modules.tale;

/**
 * The four TALE registries (roles, interaction types, relationship tiers, group
 * types) and how they are filled from the game config. Shared by TaleModule and
 * the Testbed, so the Testbed's DES runs on the same definitions as the game.
 */
public static class TaleRegistryLoader
{
    /**
     * Register the four registries, empty. Must happen before anything runs the
     * DES: DesSimulation.SeedNpc reads RoleRegistry through I.Get.
     */
    public static void Register()
    {
        I.Register<RoleRegistry>(() => new RoleRegistry());
        I.Register<InteractionTypeRegistry>(() => new InteractionTypeRegistry());
        I.Register<RelationshipTierRegistry>(() => new RelationshipTierRegistry());
        I.Register<GroupTypeRegistry>(() => new GroupTypeRegistry());
        Trace("TALE: RoleRegistry, InteractionTypeRegistry, RelationshipTierRegistry, and GroupTypeRegistry registered.");
    }


    /**
     * Fill the registries from /roles, /interactions, /relationships/tiers and
     * /groups/types once the loader has them.
     */
    public static void LoadFromConfig(engine.casette.Loader loader)
    {
        loader.WhenLoaded("/roles", (path, rolesNode) =>
        {
            var registry = I.Get<RoleRegistry>();
            if (rolesNode is JsonArray rolesArray)
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                options.Converters.Add(new ValueTupleFloatFloatConverter());
                foreach (JsonNode roleNode in rolesArray)
                {
                    var def = JsonSerializer.Deserialize<RoleDefinition>(roleNode.ToJsonString(), options);
                    if (def != null && !string.IsNullOrEmpty(def.Id))
                        registry.Add(def.Id, def);
                    else if (def != null)
                        Warning($"TALE: Role definition has null or empty Id: {roleNode.ToJsonString()}");
                }
                Trace($"TALE: Loaded {rolesArray.Count} role definitions.");
            }
            else
                Warning($"TALE: /roles is not a JsonArray, got {rolesNode?.GetType().Name ?? "null"}");
        }, 100);

        loader.WhenLoaded("/interactions", (path, interactionsNode) =>
        {
            var registry = I.Get<InteractionTypeRegistry>();
            if (interactionsNode is JsonArray interactionsArray)
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                foreach (JsonNode typeNode in interactionsArray)
                {
                    var def = JsonSerializer.Deserialize<InteractionTypeDefinition>(typeNode.ToJsonString(), options);
                    if (def != null && !string.IsNullOrEmpty(def.Id))
                        registry.Add(def.Id, def);
                    else if (def != null)
                        Warning($"TALE: Interaction type has null or empty Id: {typeNode.ToJsonString()}");
                }
                registry.FinalizeOrder();
                Trace($"TALE: Loaded {interactionsArray.Count} interaction type definitions.");
            }
            else
                Warning($"TALE: /interactions is not a JsonArray, got {interactionsNode?.GetType().Name ?? "null"}");
        }, 100);

        loader.WhenLoaded("/relationships/tiers", (path, tiersNode) =>
        {
            var registry = I.Get<RelationshipTierRegistry>();
            if (tiersNode is JsonArray tiersArray)
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                foreach (JsonNode tierNode in tiersArray)
                {
                    var def = JsonSerializer.Deserialize<RelationshipTierDefinition>(tierNode.ToJsonString(), options);
                    if (def != null && !string.IsNullOrEmpty(def.Id))
                        registry.Add(def.Id, def);
                    else if (def != null)
                        Warning($"TALE: Relationship tier has null or empty Id: {tierNode.ToJsonString()}");
                }
                Trace($"TALE: Loaded {tiersArray.Count} relationship tier definitions.");
            }
            else
                Warning($"TALE: /relationships/tiers is not a JsonArray, got {tiersNode?.GetType().Name ?? "null"}");
        }, 100);

        loader.WhenLoaded("/groups/types", (path, typesNode) =>
        {
            var registry = I.Get<GroupTypeRegistry>();
            if (typesNode is JsonArray typesArray)
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                foreach (JsonNode typeNode in typesArray)
                {
                    var def = JsonSerializer.Deserialize<GroupTypeDefinition>(typeNode.ToJsonString(), options);
                    if (def != null && !string.IsNullOrEmpty(def.Id))
                        registry.Add(def.Id, def);
                    else if (def != null)
                        Warning($"TALE: Group type has null or empty Id: {typeNode.ToJsonString()}");
                }
                Trace($"TALE: Loaded {typesArray.Count} group type definitions.");
            }
            else
                Warning($"TALE: /groups/types is not a JsonArray, got {typesNode?.GetType().Name ?? "null"}");
        }, 100);
    }
}
