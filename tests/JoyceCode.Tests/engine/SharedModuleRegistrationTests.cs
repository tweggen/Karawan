using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace JoyceCode.Tests.engine;


/**
 * A SharedModule<T> is built by ModuleFactory through I.Instance.GetInstance(typeof(T)),
 * which knows a type only if it was registered: as a key of models/nogame.implementations.json
 * or by an I.Register<T> call in code. A dependency on an unregistered module fails at
 * runtime, on the first scene that lists it, with nothing more than "Unable to instantiate
 * moduleDependency" (ModuleTracker).
 *
 * Found when KAR-475 added HelloFishmongerModule to the root scene without registering it:
 * every game start failed, and no test noticed.
 */
public class SharedModuleRegistrationTests
{
    private static string Root => Path.GetFullPath(Path.Combine(global::engine.GameRoot.PathTo("JoyceCode"), ".."));


    [Fact]
    public void EverySharedModuleIsRegistered()
    {
        string implementations = File.ReadAllText(Path.Combine(Root, "models/nogame.implementations.json"));
        var registeredInJson = Regex.Matches(implementations, @"^\s*""([A-Za-z0-9_.]+)""\s*:", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value).ToHashSet();

        var sources = new[] { "JoyceCode", "nogameCode" }
            .SelectMany(d => Directory.EnumerateFiles(Path.Combine(Root, d), "*.cs", SearchOption.AllDirectories))
            .Select(File.ReadAllText).ToList();
        string allSource = string.Join("\n", sources);

        var shared = sources
            .SelectMany(s => Regex.Matches(s, @"SharedModule<\s*([A-Za-z0-9_.]+)\s*>").Select(m => m.Groups[1].Value))
            .Where(t => t != "T")
            .ToHashSet();
        Assert.NotEmpty(shared);

        // Call sites often name a module by its short name, with a using; match on that.
        bool IsRegistered(string t) =>
            registeredInJson.Any(k => k == t || k.EndsWith("." + t))
            || Regex.IsMatch(allSource, @"I\.Register<\s*([A-Za-z0-9_]+\.)*" + Regex.Escape(t) + @"\s*>");

        var missing = shared
            .Where(t => !IsRegistered(t))
            .OrderBy(t => t)
            .ToList();

        Assert.True(missing.Count == 0,
            "used as SharedModule<T> but registered nowhere (add it to models/nogame.implementations.json): "
            + string.Join(", ", missing));
    }
}
