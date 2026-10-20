using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using engine;
using engine.tale;
using engine.world;

namespace Testbed;

/// <summary>
/// TALE S0 W9 (KAR-452): what the player meets on arrival, headless.
/// Populates real clusters exactly as the game does (shipped storylets and
/// registries, scenario bake, warm-up), then lets the society live for some
/// game days with the background tick (W5) and the encounter/re-detect
/// cadence of TaleModule, and records metrics, NPC density and property
/// drift. The JSON on stdout is deterministic (two runs are identical);
/// timings go to stderr.
/// </summary>
public static class TaleArrival
{
    private const float Radius = 100f;
    private static readonly TimeSpan Step = TimeSpan.FromMinutes(10);
    private const double RedetectHours = 2.0;

    private static readonly string[] DriftProperties =
        { "hunger", "fatigue", "health", "happiness", "wealth", "fear", "anger", "morality", "reputation" };


    public static string Run(string clusterSelection, int days, Action<string> log)
    {
        var library = new StoryletLibrary();
        var tags = StoryletLibrary.CollectDeclaredTags();
        int loaded = library.LoadFromAssets(tags, out var failed);
        log($"TALE arrival: {loaded} storylet resources loaded, {failed.Count} failed, {library.All.Count} storylets.");

        I.Register<engine.tale.bake.ScenarioLibrary>(() => new engine.tale.bake.ScenarioLibrary());
        I.Register<engine.tale.bake.ScenarioSelector>(() => new engine.tale.bake.ScenarioSelector());
        I.Register<engine.tale.bake.ScenarioApplicator>(() => new engine.tale.bake.ScenarioApplicator());

        var clusters = Select(clusterSelection, log, out var sizeDistribution);
        var results = new List<Dictionary<string, object>>();
        foreach (var (label, cd) in clusters)
        {
            results.Add(_measure(label, cd, library, days, log));
        }

        return JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["arrival"] = nogame.GameState.GameT0.ToString("yyyy-MM-dd HH:mm"),
            ["days"] = days,
            ["radius_m"] = Radius,
            ["cluster_npc_counts"] = sizeDistribution,
            ["clusters"] = results
        }, new JsonSerializerOptions { WriteIndented = true });
    }


    /*
     * small / medium / large: the clusters with the fewest, the median and the
     * most NPCs; "all" for those three; anything else is a cluster name.
     */
    public static List<(string Label, ClusterDesc Cluster)> Select(
        string selection, Action<string> log, out Dictionary<string, object> sizeDistribution)
    {
        var all = I.Get<ClusterList>().GetClusterList();
        var generator = new TalePopulationGenerator();
        var bySize = all.Select(cd => (cd, n: generator.ComputeNpcCount(cd)))
            .OrderBy(x => x.n).ThenBy(x => x.cd.Index).ToList();
        log($"TALE arrival: {all.Count} clusters, NPC counts {bySize[0].n}..{bySize[^1].n}.");
        var counts = bySize.Select(x => x.n).ToList();
        sizeDistribution = new Dictionary<string, object>
        {
            ["clusters"] = counts.Count,
            ["at_cap_4095"] = counts.Count(n => n >= 4095),
            ["min"] = counts[0],
            ["p25"] = counts[counts.Count / 4],
            ["median"] = counts[counts.Count / 2],
            ["p75"] = counts[counts.Count * 3 / 4],
            ["max"] = counts[^1],
            ["total_npcs"] = counts.Sum()
        };

        var small = ("small", bySize[0].cd);
        var medium = ("medium", bySize[bySize.Count / 2].cd);
        var large = ("large", bySize[^1].cd);
        return selection switch
        {
            "small" => new() { small },
            "medium" => new() { medium },
            "large" => new() { large },
            "all" or null or "" => new() { small, medium, large },
            _ => all.Where(cd => cd.Name == selection).Select(cd => (cd.Name, cd)).ToList()
        };
    }


    private static Dictionary<string, object> _measure(
        string label, ClusterDesc cd, StoryletLibrary library, int days, Action<string> log)
    {
        var tm = new TaleManager();
        tm.Initialize(library);
        tm.GroupNameProvider = nogame.modules.tale.GroupNameGenerator.Generate;
        int ci = cd.Index;
        DateTime arrival = nogame.GameState.GameT0;

        var sw = Stopwatch.StartNew();
        var model = SpatialModel.ExtractFrom(cd);
        long extractMs = sw.ElapsedMilliseconds;

        sw.Restart();
        tm.PopulateCluster(cd, model, arrival);
        long populateMs = sw.ElapsedMilliseconds;
        var npcs = tm.GetNpcsInCluster(ci);
        log($"[{label}] cluster {ci} \"{cd.Name}\": {npcs.Count} NPCs, {model.Locations.Count} locations; " +
            $"spatial model {extractMs} ms, populate {populateMs} ms.");

        Vector3 startPos = cd.FindStartPose().V3World;
        var streetLocations = model.Locations.Where(l => l.Type == "street_segment").ToList();
        var initialProps = _propertyMeans(npcs);
        var atArrival = tm.ComputeSocialMetrics(ci).ToDictionary();

        var evolver = new RuntimeSocialEvolver();
        DateTime lastRedetect = arrival;
        var hourly = new List<Dictionary<string, object>>();
        Dictionary<string, object> afterDay1 = null, propsDay1 = null;
        long tickTicks = 0, evolveTicks = 0;
        int totalSteps = 0;
        DateTime lastDate = arrival.Date;

        for (DateTime now = arrival; now <= arrival.AddDays(days); now += Step)
        {
            if (now > arrival)
            {
                long t0 = Stopwatch.GetTimestamp();
                totalSteps += tm.CatchUpCluster(ci, now).Steps;
                long t1 = Stopwatch.GetTimestamp();
                evolver.TickEncounters(ci, tm.GetNpcsInCluster(ci), model, now);
                if ((now - lastRedetect).TotalHours >= RedetectHours)
                {
                    lastRedetect = now;
                    evolver.Redetect(ci, tm.GetSocialState(ci),
                        tm.GetNpcsInCluster(ci).ToDictionary(n => n.NpcId),
                        nogame.modules.tale.GroupNameGenerator.Generate);
                }
                tickTicks += t1 - t0;
                evolveTicks += Stopwatch.GetTimestamp() - t1;

                if (now.Date != lastDate)
                {
                    tm.RollSocialMetricsDay(ci, now);
                    lastDate = now.Date;
                }
            }

            var elapsed = now - arrival;
            if (elapsed <= TimeSpan.FromDays(1) && now.Minute == 0)
            {
                hourly.Add(_densitySample(tm.GetNpcsInCluster(ci), model, now, startPos, streetLocations));
            }
            if (elapsed == TimeSpan.FromDays(1))
            {
                afterDay1 = tm.ComputeSocialMetrics(ci).ToDictionary();
                propsDay1 = _propertyMeans(tm.GetNpcsInCluster(ci));
            }
        }

        double tickMs = tickTicks * 1000.0 / Stopwatch.Frequency;
        double evolveMs = evolveTicks * 1000.0 / Stopwatch.Frequency;
        log($"[{label}] {days} game days: catch-up {tickMs:F0} ms ({totalSteps} steps, " +
            $"{tickMs / days:F1} ms per game day), encounters+redetect {evolveMs:F0} ms.");

        var afterAll = tm.ComputeSocialMetrics(ci).ToDictionary();
        var propsAll = _propertyMeans(tm.GetNpcsInCluster(ci));

        // Leave and return (W4): the snapshot size, and the repopulation time.
        tm.DepopulateCluster(ci);
        int socialStateChars = tm.GetSocialState(ci).ToJson().ToJsonString().Length;
        sw.Restart();
        tm.PopulateCluster(cd, model, arrival.AddDays(days));
        log($"[{label}] repopulate {sw.ElapsedMilliseconds} ms; social state {socialStateChars} chars.");

        return new Dictionary<string, object>
        {
            ["label"] = label,
            ["cluster_index"] = ci,
            ["name"] = cd.Name,
            ["npcs"] = npcs.Count,
            ["locations"] = model.Locations.Count,
            ["street_locations"] = streetLocations.Count,
            ["steps_per_npc_per_day"] = Math.Round((double)totalSteps / npcs.Count / days, 2),
            ["metrics_at_arrival"] = atArrival,
            ["metrics_after_1_day"] = afterDay1,
            ["metrics_at_end"] = afterAll,
            ["density_hourly_day1"] = hourly,
            ["property_means"] = new Dictionary<string, object>
            {
                ["arrival"] = initialProps,
                ["after_1_day"] = propsDay1,
                ["at_end"] = propsAll
            },
            ["social_state_json_chars"] = socialStateChars
        };
    }


    private static Dictionary<string, object> _densitySample(
        List<NpcSchedule> npcs, SpatialModel model, DateTime now, Vector3 startPos, List<Location> streets)
    {
        var located = NpcDensityCounter.Locate(npcs, model, now);
        var atStart = NpcDensityCounter.CountNear(located, startPos, Radius);

        var streetCounts = streets
            .Select(l => NpcDensityCounter.CountNear(located, l.Position, Radius))
            .ToList();
        var visible = streetCounts.Select(d => d.Street).OrderBy(x => x).ToList();

        return new Dictionary<string, object>
        {
            ["time"] = now.ToString("HH:mm"),
            ["at_start_pose"] = new Dictionary<string, object>
            {
                ["total"] = atStart.Total, ["street"] = atStart.Street,
                ["venue"] = atStart.Venue, ["home_or_work"] = atStart.HomeOrWork
            },
            ["over_street_locations"] = visible.Count == 0 ? null : new Dictionary<string, object>
            {
                ["mean_total"] = Math.Round(streetCounts.Average(d => d.Total), 2),
                ["mean_street"] = Math.Round(visible.Average(), 2),
                ["median_street"] = visible[visible.Count / 2],
                ["max_street"] = visible[^1],
                ["share_with_4plus_street"] = Math.Round(visible.Count(v => v >= 4) / (double)visible.Count, 3)
            }
        };
    }


    private static Dictionary<string, object> _propertyMeans(List<NpcSchedule> npcs)
    {
        var result = new Dictionary<string, object>();
        foreach (var key in DriftProperties)
        {
            var values = npcs.Where(n => n.Properties != null && n.Properties.ContainsKey(key))
                .Select(n => (double)n.Properties[key]).ToList();
            if (values.Count == 0) continue;
            result[key] = Math.Round(values.Average(), 3);
        }
        return result;
    }
}
