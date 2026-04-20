using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static engine.Logger;

namespace engine.tale.bake;

/// <summary>
/// Runtime catalog of pre-baked TALE social-structure scenarios. Mirrors the
/// role <c>Model.BakeAnimations</c> + <c>TryLoadModelAnimationCollection</c> play
/// for animations: try the on-disk baked artifact first, fall through to in-process
/// generation when the file is missing, parse fails, or the global override
/// <c>joyce.DisablePrebakedScenarios</c> is set.
///
/// Discovered scenarios are read from <see cref="AAssetImplementation.AvailableScenarios"/>,
/// which is populated by <c>_whenLoadedScenarios</c> when the engine config is
/// interpreted. The library does not eagerly load anything — each scenario is
/// pulled on first request and cached for the rest of the process lifetime.
///
/// In-process regeneration is NOT written back to <c>generated/</c>: the bake
/// artifact is the seeded contract between build and runtime, and writing it
/// from a non-Chushi context could break that contract. The fallback exists so
/// the game keeps working after an incomplete or skipped build, not as a way
/// to bypass Chushi.
/// </summary>
public class ScenarioLibrary
{
    private static readonly engine.Dc _dc = engine.Dc.TaleManager;

    private readonly Dictionary<(string, int), Scenario> _cache = new();
    private readonly object _lock = new();


    /// <summary>
    /// Available scenario bake requests, snapshotted from the asset
    /// implementation at first access. Returns an empty list if no asset
    /// implementation is registered or it isn't an <see cref="AAssetImplementation"/>.
    /// </summary>
    public IReadOnlyList<AAssetImplementation.ScenarioBakeRequest> AvailableScenarios
    {
        get
        {
            var impl = engine.Assets.GetAssetImplementation() as AAssetImplementation;
            if (impl == null) return Array.Empty<AAssetImplementation.ScenarioBakeRequest>();
            return impl.AvailableScenarios;
        }
    }


    /// <summary>
    /// Look up a scenario by category name and index. Returns true if a
    /// scenario was loaded (from disk) or generated (in-process fallback).
    /// Returns false only if the requested (category, index) is not declared
    /// in the engine config at all — i.e. there is no matching bake request.
    /// </summary>
    public bool TryGet(string category, int index, out Scenario scenario)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue((category, index), out scenario))
                return true;
        }

        // Find the bake request so we know seed + npcCount + simulation days.
        AAssetImplementation.ScenarioBakeRequest req = null;
        foreach (var r in AvailableScenarios)
        {
            if (r.CategoryName == category && r.Index == index)
            {
                req = r;
                break;
            }
        }
        if (req == null)
        {
            Trace(_dc, $"ScenarioLibrary: no bake request declared for {category}/{index}.");
            scenario = null;
            return false;
        }

        scenario = LoadOrBake(req);
        if (scenario != null)
        {
            lock (_lock)
            {
                _cache[(category, index)] = scenario;
            }
            return true;
        }
        return false;
    }


    /// <summary>
    /// Convenience overload for callers that already have the bake request in
    /// hand (e.g. ScenarioSelector). Skips the AvailableScenarios lookup.
    /// </summary>
    public Scenario GetOrLoad(AAssetImplementation.ScenarioBakeRequest req)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue((req.CategoryName, req.Index), out var cached))
                return cached;
        }
        var scenario = LoadOrBake(req);
        if (scenario != null)
        {
            lock (_lock)
            {
                _cache[(req.CategoryName, req.Index)] = scenario;
            }
        }
        return scenario;
    }


    /// <summary>
    /// The two-step try-load-then-bake pipeline. Layered exactly like
    /// <c>Model.BakeAnimations</c> at JoyceCode/engine/joyce/Model.cs:207-237.
    /// </summary>
    private Scenario LoadOrBake(AAssetImplementation.ScenarioBakeRequest req)
    {
        // Step 1: probe the baked file unless explicitly disabled.
        bool disabled = engine.GlobalSettings.Get("joyce.DisablePrebakedScenarios") == "true";
        if (!disabled)
        {
            var fromDisk = TryLoadFromDisk(req);
            if (fromDisk != null)
            {
                Trace(_dc, $"ScenarioLibrary: loaded baked {req.CategoryName}/{req.Index} from disk.");
                return fromDisk;
            }
        }
        else
        {
            Trace(_dc, $"ScenarioLibrary: prebaked scenarios disabled via joyce.DisablePrebakedScenarios; baking {req.CategoryName}/{req.Index} in-process.");
        }

        // Step 2: in-process bake. Mirrors AnimationCollection.BakeAnimations
        // being called from Model.BakeAnimations when TryLoadModelAnimationCollection fails.
        try
        {
            var compiler = new ScenarioCompiler
            {
                CategoryName = req.CategoryName,
                Index = req.Index,
                NpcCount = req.NpcCount,
                Seed = req.Seed,
                SimulationDays = req.SimulationDays > 0 ? req.SimulationDays : 365,
                OutputDirectory = "" // not used by CompileInMemory
            };
            var baked = compiler.CompileInMemory();
            Trace(_dc, $"ScenarioLibrary: manually generated scenario for {req.CategoryName}/{req.Index} ({baked.NpcCount} npcs, {baked.Groups.Count} groups, {baked.Relationships.Count} relationships).");
            return baked;
        }
        catch (Exception e)
        {
            Warning($"ScenarioLibrary: in-process scenario generation for {req.CategoryName}/{req.Index} failed: {e}");
            return null;
        }
    }


    private Scenario TryLoadFromDisk(AAssetImplementation.ScenarioBakeRequest req)
    {
        string fileName = ScenarioFileName.Of(req.CategoryName, req.Index, req.Seed);
        Stream stream = null;
        try
        {
            stream = engine.Assets.Open(fileName);
            if (stream == null || stream.Length == 0)
            {
                if (stream != null) stream.Dispose();
                return null;
            }
            return ScenarioExporter.ReadFromStream(stream);
        }
        catch (Exception e)
        {
            Trace(_dc, $"ScenarioLibrary: could not read baked scenario file {fileName}: {e.Message}");
            return null;
        }
        finally
        {
            stream?.Dispose();
        }
    }


    /// <summary>
    /// Diagnostic helper: how many scenarios have been resolved (loaded or
    /// generated) and are sitting in the in-memory cache right now.
    /// </summary>
    public int CachedCount
    {
        get
        {
            lock (_lock)
            {
                return _cache.Count;
            }
        }
    }
}
