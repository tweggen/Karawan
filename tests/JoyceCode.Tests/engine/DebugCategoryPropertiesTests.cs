using System;
using System.IO;
using System.Linq;
using Xunit;

namespace JoyceCode.Tests.engine;

/// <summary>
/// Every debug category needs its "debug.category.{name}" switch in
/// models/nogame.properties.json, or DebugFilter can never enable it and its
/// Trace lines are lost for good. Dc.TaleSocial went without one for months,
/// silencing the runtime social tick and the S1 player-interaction traces.
/// </summary>
public class DebugCategoryPropertiesTests
{
    private static string PropertiesJson()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "models", "nogame.properties.json")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(dir!.FullName, "models", "nogame.properties.json"));
    }


    [Fact]
    public void EveryDebugCategory_HasASwitchInThePropertiesFile()
    {
        string json = PropertiesJson();
        var missing = Enum.GetNames<global::engine.Dc>()
            .Where(name => name != nameof(global::engine.Dc._Count))
            .Where(name => !json.Contains($"\"debug.category.{name.ToLowerInvariant()}\""))
            .ToList();

        Assert.True(missing.Count == 0,
            "Debug categories without a debug.category.* entry in models/nogame.properties.json: "
            + string.Join(", ", missing));
    }
}
