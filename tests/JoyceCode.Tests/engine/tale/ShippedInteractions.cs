using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using engine.tale;
using Xunit;

namespace JoyceCode.Tests.engine.tale;

/// <summary>The shipped models/nogame.interactions.json, deserialised like TaleRegistryLoader.</summary>
internal static class ShippedInteractions
{
    public static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "models", "nogame.interactions.json")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }


    public static InteractionTypeRegistry Registry(Func<JsonNode, bool> keep = null)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var registry = new InteractionTypeRegistry();
        var json = File.ReadAllText(Path.Combine(RepoRoot(), "models", "nogame.interactions.json"));
        foreach (var node in JsonNode.Parse(json)!.AsArray())
        {
            if (keep != null && !keep(node!)) continue;
            var def = JsonSerializer.Deserialize<InteractionTypeDefinition>(node!.ToJsonString(), options);
            registry.Add(def!.Id, def);
        }
        registry.FinalizeOrder();
        return registry;
    }


    /// <summary>A TaleManager whose player interactions use the shipped types.</summary>
    public static TaleManager Manager(Func<JsonNode, bool> keep = null)
    {
        var tm = new TaleManager();
        tm.PlayerInteractions = new PlayerInteractions(tm, Registry(keep));
        return tm;
    }
}
