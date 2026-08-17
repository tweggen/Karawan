using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace JoyceCode.Tests.engine.streets;


/**
 * Reads and writes the committed street-generation baselines.
 *
 * Baselines are stored per environment stamp rather than as one portable set of
 * numbers: floating point results are only guaranteed reproducible for a given
 * runtime and architecture, and this repository is built on several. A missing
 * baseline for the current environment is reported as a failure with instructions,
 * never silently skipped — a gate that can quietly pass everywhere is not a gate.
 *
 * Regenerate with:  JOYCE_STREET_BASELINE_WRITE=1 dotnet test ...
 */
internal static class StreetBaselines
{
    internal const string WriteEnvVar = "JOYCE_STREET_BASELINE_WRITE";

    internal static bool WriteRequested =>
        Environment.GetEnvironmentVariable(WriteEnvVar) == "1";


    /**
     * Baselines live next to the tests in the source tree, not in bin/, because the
     * write mode has to update the committed file.
     */
    internal static string PathFor(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "JoyceCode.Tests.csproj")))
        {
            dir = dir.Parent;
        }

        if (dir == null)
        {
            throw new InvalidOperationException(
                "Could not locate JoyceCode.Tests.csproj above " + AppContext.BaseDirectory);
        }

        return Path.Combine(dir.FullName, "engine", "streets", "baselines", fileName);
    }


    internal static JsonObject Load(string fileName)
    {
        string path = PathFor(fileName);
        if (!File.Exists(path))
        {
            return new JsonObject { ["environments"] = new JsonObject() };
        }

        return JsonNode.Parse(File.ReadAllText(path))!.AsObject();
    }


    internal static void Save(string fileName, JsonObject root)
    {
        string path = PathFor(fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path,
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }


    /**
     * The recorded values for this runtime, or null when this environment has never
     * been baselined.
     */
    internal static JsonObject? EntriesFor(JsonObject root, string environmentStamp)
    {
        var environments = root["environments"]?.AsObject();
        if (environments == null) return null;
        if (!environments.TryGetPropertyValue(environmentStamp, out var node)) return null;
        return node?.AsObject()?["seeds"]?.AsObject();
    }


    internal static void Record(
        JsonObject root, string environmentStamp, string key, JsonNode value)
    {
        var environments = root["environments"]?.AsObject();
        if (environments == null)
        {
            environments = new JsonObject();
            root["environments"] = environments;
        }

        if (!environments.TryGetPropertyValue(environmentStamp, out var envNode) || envNode == null)
        {
            envNode = new JsonObject { ["seeds"] = new JsonObject() };
            environments[environmentStamp] = envNode;
        }

        var seeds = envNode.AsObject()["seeds"]?.AsObject();
        if (seeds == null)
        {
            seeds = new JsonObject();
            envNode.AsObject()["seeds"] = seeds;
        }

        seeds[key] = value;
    }


    internal static string MissingBaselineMessage(string environmentStamp, string fileName)
    {
        return
            $"No baseline recorded for environment '{environmentStamp}' in {fileName}.\n" +
            $"Floating point output is only guaranteed reproducible per runtime and\n" +
            $"architecture, so each build environment records its own baseline.\n" +
            $"If this is a new environment (not a regression), regenerate with:\n" +
            $"    {WriteEnvVar}=1 dotnet test tests/JoyceCode.Tests/JoyceCode.Tests.csproj\n" +
            $"and commit the updated {fileName}.";
    }
}
