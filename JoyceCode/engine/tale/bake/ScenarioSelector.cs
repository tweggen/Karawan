using System;
using System.Collections.Generic;
using System.Linq;
using static engine.Logger;

namespace engine.tale.bake;

/// <summary>
/// Picks which baked scenario to apply to a freshly populated cluster.
/// Decoupled from <see cref="ScenarioLibrary"/>: the selector decides
/// which (category, index) tuple makes sense for a given target NPC count
/// and cluster seed; the library is responsible for actually materializing
/// the scenario data (loading or in-process baking).
///
/// Selection strategy:
/// 1. Group all available bake requests by category.
/// 2. Pick the category whose median NpcCount is closest to the target.
///    Median (rather than mean or range midpoint) is robust against
///    asymmetric category sizing and matches the Phase D plan's
///    "size category" intent.
/// 3. Within the chosen category, pick a scenario index deterministically
///    via <c>(clusterSeed mod count)</c>. This is round-robin under a
///    seeded permutation: same seed → same scenario, different seeds →
///    different scenarios spread across the category's variants.
/// </summary>
public class ScenarioSelector
{
    /// <summary>
    /// Returns the bake request that should drive scenario application for
    /// a cluster of <paramref name="targetNpcCount"/> NPCs identified by
    /// <paramref name="clusterSeed"/>. Returns null if no bake requests
    /// exist at all (config empty or scenarios disabled).
    /// </summary>
    public AAssetImplementation.ScenarioBakeRequest Pick(int targetNpcCount, int clusterSeed)
    {
        var requests = GetAvailableRequests();
        if (requests.Count == 0)
        {
            Trace("ScenarioSelector: no scenarios declared; returning null.");
            return null;
        }

        // Step 1+2: pick the category whose median NpcCount is nearest the target.
        string bestCategory = null;
        int bestDistance = int.MaxValue;
        var byCategory = new Dictionary<string, List<AAssetImplementation.ScenarioBakeRequest>>();
        foreach (var req in requests)
        {
            if (!byCategory.TryGetValue(req.CategoryName, out var list))
            {
                list = new List<AAssetImplementation.ScenarioBakeRequest>();
                byCategory[req.CategoryName] = list;
            }
            list.Add(req);
        }

        foreach (var kvp in byCategory)
        {
            var sortedCounts = kvp.Value.Select(r => r.NpcCount).OrderBy(n => n).ToList();
            int median = sortedCounts[sortedCounts.Count / 2];
            int dist = Math.Abs(median - targetNpcCount);
            if (dist < bestDistance)
            {
                bestDistance = dist;
                bestCategory = kvp.Key;
            }
        }

        if (bestCategory == null)
        {
            // Defensive: shouldn't happen given we checked requests.Count above.
            return requests[0];
        }

        // Step 3: round-robin within the chosen category, ordered by Index for stability.
        var inCategory = byCategory[bestCategory].OrderBy(r => r.Index).ToList();
        // Modulo with negative-safe correction.
        int pickIdx = ((clusterSeed % inCategory.Count) + inCategory.Count) % inCategory.Count;
        var picked = inCategory[pickIdx];

        Trace($"ScenarioSelector: target={targetNpcCount} clusterSeed={clusterSeed} → " +
              $"category={picked.CategoryName} index={picked.Index} (npcCount={picked.NpcCount})");
        return picked;
    }


    /// <summary>
    /// Convenience: pick AND load via the supplied library, in one call.
    /// Returns null if either selection or loading fails.
    /// </summary>
    public Scenario PickAndLoad(int targetNpcCount, int clusterSeed, ScenarioLibrary library)
    {
        var req = Pick(targetNpcCount, clusterSeed);
        if (req == null) return null;
        return library.GetOrLoad(req);
    }


    private static IReadOnlyList<AAssetImplementation.ScenarioBakeRequest> GetAvailableRequests()
    {
        var impl = engine.Assets.GetAssetImplementation() as AAssetImplementation;
        if (impl == null) return Array.Empty<AAssetImplementation.ScenarioBakeRequest>();
        return impl.AvailableScenarios;
    }
}
