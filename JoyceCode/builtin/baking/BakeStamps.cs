using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using static engine.Logger;

namespace builtin.baking;

/**
 * KAR-501: which baker made a baked file, so that a change of the BAKER - not only of
 * its inputs - re-bakes it.
 *
 * Chushi used to skip a bake whenever the output was newer than its source files. The
 * baker's own code (Chushi, Mazu, JoyceFbx/Assimp, the baking parts of JoyceCode) was not
 * part of that decision, and nogame/generated/ is not under bin/obj, so a file baked by an
 * old baker survived every clean build under a name that looks current. Measured
 * 2026-11-25 on Linux: rebaking files from 2026-08-20 changed the content of 13 of 14
 * animation files and 3 of 13 models.
 *
 * Every baked file now carries a stamp in one manifest next to the outputs; a file whose
 * recorded stamp differs from the running baker's is re-baked.
 *
 * Do NOT compare bakes by file bytes or size: the Lz4BlockArray-compressed output is not
 * byte-stable even when the content is identical (KAR-502).
 */
public static class BakeVersions
{
    /**
     * Bump when the mo- format or what the model bake writes changes on purpose.
     * Any change of the baker assemblies re-bakes models anyway (see BakerIdentity).
     */
    public const int Model = 1;

    /** As Model, for the ac- animation collections. */
    public const int Animation = 1;

    /**
     * Scenarios are NOT keyed on the baker assemblies: a full scenario bake takes about an
     * hour and the DES code changes with nearly every engine commit. Bump this to re-bake
     * them after a change to the simulation, the selector or the classifier. Version 1
     * adopts scenario bakes made before stamps existed.
     */
    public const int Scenario = 1;
}


public static class BakerIdentity
{
    /**
     * A hash over every file the baker runs from (the published Chushi directory: its own
     * assemblies, Joyce, Mazu, JoyceFbx, the native importer and the runtime). Debug
     * symbols and XML docs are left out; they can change without the code changing.
     * Builds are deterministic, so an unchanged source tree hashes the same.
     */
    public static string HashDirectory(string directory)
    {
        var root = Path.GetFullPath(directory);
        var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Where(IsBakerFile)
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var rel in files)
        {
            sha.AppendData(Encoding.UTF8.GetBytes(rel));
            sha.AppendData(new byte[] { 0 });
            sha.AppendData(File.ReadAllBytes(Path.Combine(root, rel)));
        }
        return Convert.ToHexString(sha.GetHashAndReset()).Substring(0, 16).ToLowerInvariant();
    }


    public static bool IsBakerFile(string path)
    {
        string ext = Path.GetExtension(path);
        return !ext.Equals(".pdb", StringComparison.OrdinalIgnoreCase)
               && !ext.Equals(".xml", StringComparison.OrdinalIgnoreCase);
    }


    public static string ModelStamp(string bakerHash) => $"model-v{BakeVersions.Model}-{bakerHash}";
    public static string AnimationStamp(string bakerHash) => $"animation-v{BakeVersions.Animation}-{bakerHash}";
    public static string ScenarioStamp() => $"scenario-v{BakeVersions.Scenario}";
}


/**
 * The stamps of the files in one output directory, kept in bake-manifest.json there.
 * The file is a build-tool artefact: it is not declared as a resource, so it never ships.
 * Thread-safe: bakes run in parallel and record themselves as they finish.
 */
public sealed class BakeManifest
{
    public const string FileName = "bake-manifest.json";

    private readonly object _lo = new();
    private readonly SortedDictionary<string, string> _stamps;


    private BakeManifest(SortedDictionary<string, string> stamps) => _stamps = stamps;


    public static BakeManifest Empty() => new(new SortedDictionary<string, string>(StringComparer.Ordinal));


    /** A missing or unreadable manifest is an empty one: everything re-bakes. */
    public static BakeManifest Load(string directory)
    {
        string path = Path.Combine(directory, FileName);
        if (!File.Exists(path)) return Empty();
        try
        {
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
            return map == null
                ? Empty()
                : new BakeManifest(new SortedDictionary<string, string>(map, StringComparer.Ordinal));
        }
        catch (Exception e)
        {
            Warning($"Unreadable bake manifest {path}, treating every bake as unstamped: {e.Message}");
            return Empty();
        }
    }


    public string? StampOf(string fileName)
    {
        lock (_lo) return _stamps.TryGetValue(fileName, out var s) ? s : null;
    }


    public void Record(string fileName, string stamp)
    {
        lock (_lo) _stamps[fileName] = stamp;
    }


    /**
     * Forget a file's stamp. Done for every file about to be baked, and saved, BEFORE the
     * bake starts: a bake that dies half-way must not leave a partial output next to a
     * stamp that still vouches for it.
     */
    public void Forget(string fileName)
    {
        lock (_lo) _stamps.Remove(fileName);
    }


    /** Written to a temporary file and moved into place, so a crash never leaves half a manifest. */
    public void Save(string directory)
    {
        string json;
        lock (_lo) json = JsonSerializer.Serialize(_stamps, new JsonSerializerOptions { WriteIndented = true });
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, FileName);
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, path, overwrite: true);
    }
}


/** Whether one baked file must be (re-)baked, and why. */
public readonly record struct BakeDecision(bool Bake, string Reason)
{
    /**
     * <paramref name="staleReason"/> is the input check's answer (output missing, a source
     * newer than the output), null when the inputs are older. <paramref name="adoptUnstamped"/>
     * accepts an output that has no stamp yet as the current version - only for scenarios
     * at BakeVersions.Scenario 1, whose re-bake costs an hour.
     */
    public static BakeDecision Of(string? staleReason, string? recordedStamp, string currentStamp,
        bool adoptUnstamped = false)
    {
        if (staleReason != null) return new BakeDecision(true, staleReason);
        if (recordedStamp == null)
        {
            return adoptUnstamped
                ? new BakeDecision(false, $"up to date (unstamped, adopted as {currentStamp})")
                : new BakeDecision(true, "no baker stamp recorded");
        }
        if (recordedStamp != currentStamp)
            return new BakeDecision(true, $"baked by {recordedStamp}, baker is now {currentStamp}");
        return new BakeDecision(false, "up to date");
    }
}
