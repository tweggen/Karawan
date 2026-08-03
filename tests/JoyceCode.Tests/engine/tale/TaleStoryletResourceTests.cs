using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace JoyceCode.Tests.engine.tale;

/**
 * Guards the declaration of the TALE storylet libraries in models/nogame.resources.json.
 *
 * The storylets are ordinary files on disk during a from-source desktop run, which makes
 * it easy to add one and never notice that it is invisible everywhere else: the resource
 * compiler only emits AndroidResources.xml / InnoResources.iss entries for what the
 * resource list names, and TaleModule discovers what to load by filtering that same list
 * on type "taleStorylet". An undeclared file therefore ships nowhere and loads nowhere -
 * silently, because a smaller storylet library is still a working one.
 *
 * Android flattens every asset to its basename, so the tags must be unique across the
 * whole resource list, not just within models/tale/.
 */
public class TaleStoryletResourceTests
{
    private const string StoryletType = "taleStorylet";


    private static string? _findModelsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, "models");
            if (File.Exists(Path.Combine(candidate, "nogame.resources.json")))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }


    private static JsonArray _loadResourceList(string modelsDir)
    {
        var node = JsonNode.Parse(
            File.ReadAllText(Path.Combine(modelsDir, "nogame.resources.json")),
            documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true });

        var list = node?["list"] as JsonArray;
        Assert.NotNull(list);
        return list!;
    }


    /**
     * The uris are written relative to the nogame project directory ("../models/x"),
     * which is how every other entry in the list spells them.
     */
    private static string _expectedUri(string fileName) => $"../models/tale/{fileName}";


    [Fact]
    public void EveryStoryletFileIsDeclaredAsAResource()
    {
        string? modelsDir = _findModelsDir();
        Assert.True(modelsDir != null, "models/ directory not found from the test output directory.");

        var declared = _loadResourceList(modelsDir!)
            .Where(n => n?["type"]?.GetValue<string>() == StoryletType)
            .Select(n => n!["uri"]!.GetValue<string>())
            .ToHashSet();

        var onDisk = Directory
            .GetFiles(Path.Combine(modelsDir!, "tale"), "*.json")
            .Select(Path.GetFileName)
            .ToList();

        Assert.NotEmpty(onDisk);

        var undeclared = onDisk.Where(f => !declared.Contains(_expectedUri(f!))).ToList();
        Assert.True(undeclared.Count == 0,
            "Storylet files present in models/tale/ but not declared in nogame.resources.json "
            + $"with type \"{StoryletType}\" (they would ship with neither the APK nor the "
            + $"installer, and TaleModule would not load them): {string.Join(", ", undeclared)}");
    }


    [Fact]
    public void EveryDeclaredStoryletResourceExists()
    {
        string? modelsDir = _findModelsDir();
        Assert.True(modelsDir != null, "models/ directory not found from the test output directory.");

        // The nogame project directory is what "../models/..." is relative to.
        string nogameDir = Path.Combine(Path.GetDirectoryName(modelsDir!)!, "nogame");

        var missing = _loadResourceList(modelsDir!)
            .Where(n => n?["type"]?.GetValue<string>() == StoryletType)
            .Select(n => n!["uri"]!.GetValue<string>())
            .Where(uri => !File.Exists(Path.GetFullPath(Path.Combine(nogameDir, uri))))
            .ToList();

        Assert.True(missing.Count == 0,
            $"Declared storylet resources with no file on disk: {string.Join(", ", missing)}");
    }


    [Fact]
    public void StoryletTagsDoNotCollideWithOtherResources()
    {
        string? modelsDir = _findModelsDir();
        Assert.True(modelsDir != null, "models/ directory not found from the test output directory.");

        var byTag = new Dictionary<string, List<string>>();
        foreach (var entry in _loadResourceList(modelsDir!))
        {
            string? uri = entry?["uri"]?.GetValue<string>();
            if (string.IsNullOrEmpty(uri)) continue;

            string tag = entry?["tag"]?.GetValue<string>() ?? Path.GetFileName(uri);
            if (!byTag.TryGetValue(tag, out var uris))
            {
                uris = new List<string>();
                byTag[tag] = uris;
            }
            uris.Add(uri!);
        }

        var storyletTags = _loadResourceList(modelsDir!)
            .Where(n => n?["type"]?.GetValue<string>() == StoryletType)
            .Select(n => Path.GetFileName(n!["uri"]!.GetValue<string>()))
            .ToList();

        var collisions = storyletTags
            .Where(t => byTag[t].Distinct().Count() > 1)
            .Select(t => $"{t} -> [{string.Join(" | ", byTag[t].Distinct())}]")
            .ToList();

        Assert.True(collisions.Count == 0,
            "Storylet asset tags collide with other resources. Android flattens assets to "
            + $"their basename, so one would shadow the other: {string.Join("; ", collisions)}");
    }
}
