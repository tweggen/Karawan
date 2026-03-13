using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using engine;
using engine.tale;
using engine.world;

namespace Testbed;

public class TestbedMain
{
    // CLI options
    private static int _days = 7;
    private static int _seed = 42;
    private static int _npcCount = 500;
    private static int _traceCount = 5;
    private static string _eventsFile = "events.jsonl";
    private static string _traceFile = "traces.log";
    private static string _graphFile = "graph.json";
    private static string _targetsFile = "";
    private static bool _quiet;


    private static string _determineResourcePath()
    {
        if (File.Exists("./models/nogame.json")) return "./models/";
        return "../../../../../models/";
    }


    private static void _setupPlatformGraphics()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            GlobalSettings.Set("platform.threeD.API", "OpenGL");
            GlobalSettings.Set("platform.threeD.API.version", "410");
        }
        else
        {
            GlobalSettings.Set("platform.threeD.API", "OpenGL");
            GlobalSettings.Set("platform.threeD.API.version", "430");
        }
        GlobalSettings.Set("engine.NailLogicalFPS", "true");
    }


    private static string[] ParseArgs(string[] args)
    {
        var filtered = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--days" when i + 1 < args.Length:
                    _days = int.Parse(args[++i]); break;
                case "--seed" when i + 1 < args.Length:
                    _seed = int.Parse(args[++i]); break;
                case "--npcs" when i + 1 < args.Length:
                    _npcCount = int.Parse(args[++i]); break;
                case "--traces" when i + 1 < args.Length:
                    _traceCount = int.Parse(args[++i]); break;
                case "--events-file" when i + 1 < args.Length:
                    _eventsFile = args[++i]; break;
                case "--trace-file" when i + 1 < args.Length:
                    _traceFile = args[++i]; break;
                case "--graph-file" when i + 1 < args.Length:
                    _graphFile = args[++i]; break;
                case "--targets" when i + 1 < args.Length:
                    _targetsFile = args[++i]; break;
                case "--quiet":
                    _quiet = true; break;
                default:
                    filtered.Add(args[i]); break;
            }
        }
        return filtered.ToArray();
    }


    private static void Log(string msg)
    {
        if (!_quiet) Console.Error.WriteLine(msg);
    }


    public static void Main(string[] args)
    {
        args = ParseArgs(args);

        Log("=== Testbed: Phase 0A - World Generation & Spatial Model ===");

        _setupPlatformGraphics();

        string resourcePath = _determineResourcePath();
        GlobalSettings.Set("Engine.ResourcePath", resourcePath);
        Log($"Resource path: {Path.GetFullPath(resourcePath)}");

        var launchConfig = LaunchConfig.LoadFromStandardLocations(resourcePath);

        string generatedPath = Directory.Exists("assets") ? "./" : "../nogame/generated";
        GlobalSettings.Set("Engine.GeneratedResourcePath", generatedPath);
        launchConfig.ApplyToGlobalSettings();

        I.Register<engine.joyce.TextureCatalogue>(() => new engine.joyce.TextureCatalogue());

        string gameConfigPath = Path.GetFullPath(
            Path.Combine(resourcePath, launchConfig.Game.ConfigPath));
        Log($"Loading game config from: {gameConfigPath}");

        I.Register<engine.casette.Loader>(() =>
        {
            using var streamJson = File.OpenRead(gameConfigPath);
            return new engine.casette.Loader(streamJson);
        });

        var iasset = new AssetImplementation();
        iasset.WithLoader();
        I.Get<engine.casette.Loader>().InterpretConfig();

        Props.Set("nogame.CreateTrees", false);
        Props.Set("nogame.CreatePolytopes", false);
        Props.Set("world.CreateStreetAnnotations", false);
        Props.Set("world.CreateCubeCharacters", false);
        Props.Set("world.CreateTramCharacters", false);

        var e = Splash.Silk.Platform.EasyCreateHeadless(args, out var _);
        e.ExecuteLogicalThreadOnly();
        e.CallOnPlatformAvailable();

        {
            engine.ConsoleLogger logger = new(e);
            engine.Logger.SetLogTarget(logger);
        }

        Log("Engine created headlessly. Setting up world generation...");

        ClusterDesc targetCluster = null;
        int fragmentCount = 0;

        e.TaskMainThread(() =>
        {
            var setupMetaGen = I.Get<nogame.SetupMetaGen>();
            setupMetaGen.PrepareMetaGen(e);

            var clusterList = I.Get<ClusterList>();
            var clusters = clusterList.GetClusterList();
            if (clusters.Count == 0)
            {
                Log("ERROR: No clusters generated.");
                return;
            }

            targetCluster = clusters[0];
            Log($"Target cluster: \"{targetCluster.Name}\" at {targetCluster.Pos}, size={targetCluster.Size}");

            var worldMetaGen = I.Get<MetaGen>();
            var worldLoader = worldMetaGen.Loader;
            var clusterViewer = new ClusterViewer(targetCluster);
            worldLoader.AddViewer(clusterViewer);
            worldLoader.WorldLoaderProvideFragments();
            fragmentCount = worldLoader.NFragments;
            Log($"Fragments loaded: {fragmentCount}");
        }).Wait();

        if (targetCluster == null)
        {
            Log("Failed to generate cluster. Exiting.");
            e.Exit();
            Environment.Exit(1);
        }

        Log("\nExtracting spatial model...");
        var spatialModel = SpatialModel.ExtractFrom(targetCluster);

        var assignments = NpcAssigner.Assign(spatialModel, seed: _seed, npcCount: _npcCount);

        if (!_quiet)
            PrintStats(targetCluster, spatialModel, assignments, fragmentCount);

        // Run Phase 0B+0C DES simulation
        int exitCode = RunDesSimulation(spatialModel, assignments);

        e.Exit();
        Log("\nTestbed exited cleanly.");
        Environment.Exit(exitCode);
    }


    private static int RunDesSimulation(SpatialModel spatialModel, List<NpcAssignment> assignments)
    {
        Log($"\n=== Phase 0B/0C: DES Simulation ({_days} days, {assignments.Count} NPCs) ===");

        // Convert assignments to NPC schedules
        var schedules = assignments.Select(a => new NpcSchedule
        {
            NpcId = a.NpcId,
            Seed = a.Seed,
            Role = a.Role.ToString(),
            HomeLocationId = a.HomeLocationId,
            WorkplaceLocationId = a.WorkplaceLocationId,
            SocialVenueIds = new List<int>(a.SocialVenueIds),
            Properties = new Dictionary<string, float>(a.Properties),
            Trust = new Dictionary<int, float>()
        }).ToList();

        // Setup simulation
        var simStart = new DateTime(2024, 1, 1, 0, 0, 0);
        var simEnd = simStart.AddDays(_days);

        // Create logger: JSONL if file specified, otherwise null
        IEventLogger logger;
        JsonlEventLogger jsonlLogger = null;
        if (!string.IsNullOrEmpty(_eventsFile) && _eventsFile != "none")
        {
            jsonlLogger = new JsonlEventLogger(_eventsFile, simStart);
            logger = jsonlLogger;
        }
        else
        {
            logger = new NullEventLogger();
        }

        var sim = new DesSimulation();

        // Select traced NPCs: one per role + random
        var tracedIds = SelectTracedNpcs(assignments, _traceCount);
        sim.SetTracedNpcs(tracedIds);

        sim.Initialize(spatialModel, schedules, logger, simStart, _seed);

        // Run and measure
        var sw = Stopwatch.StartNew();
        sim.RunUntil(simEnd);
        sw.Stop();

        jsonlLogger?.Dispose();

        Log($"\n--- DES Results ---");
        Log($"  Simulated days:     {_days}");
        Log($"  NPC count:          {assignments.Count}");
        Log($"  Total events:       {sim.EventsProcessed}");
        Log($"  Encounters:         {sim.Encounters.TotalEncounters}");
        Log($"  Relationships:      {sim.Relationships.AllRelationships.Count}");
        Log($"  Wall-clock time:    {sw.Elapsed.TotalMilliseconds:F1} ms");
        Log($"  Events/second:      {sim.EventsProcessed / Math.Max(sw.Elapsed.TotalSeconds, 0.001):F0}");

        // Write traces
        if (!string.IsNullOrEmpty(_traceFile) && _traceFile != "none")
            WriteTraces(sim, schedules, tracedIds, simStart);

        // Write graph
        if (!string.IsNullOrEmpty(_graphFile) && _graphFile != "none")
            WriteGraph(sim, schedules, spatialModel);

        // Compute and emit metrics
        var metrics = sim.Metrics.ComputeFinalMetrics(
            sim.Npcs, sim.Relationships, _days, sim.Encounters.TotalEncounters,
            sim.Encounters, spatialModel);

        string runId = $"{DateTime.Now:yyyyMMdd_HHmmss}_seed{_seed}";
        var (pass, warnings) = CheckTargets(metrics);

        var result = new Dictionary<string, object>
        {
            ["run_id"] = runId,
            ["config"] = new Dictionary<string, object>
            {
                ["cluster_index"] = 0,
                ["npc_count"] = _npcCount,
                ["days_simulated"] = _days,
                ["seed"] = _seed,
                ["encounter_probabilities"] = new Dictionary<string, object>
                {
                    ["venue"] = 0.07, ["street"] = 0.015, ["transport"] = 0.002, ["workplace"] = 0.04
                },
                ["wall_clock_ms"] = Math.Round(sw.Elapsed.TotalMilliseconds, 1)
            },
            ["metrics"] = metrics,
            ["warnings"] = warnings,
            ["pass"] = pass
        };

        var jsonOpts = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(result, jsonOpts);
        Console.Out.WriteLine(json);

        return pass ? 0 : 1;
    }


    private static List<int> SelectTracedNpcs(List<NpcAssignment> assignments, int count)
    {
        var ids = new List<int>();
        var roles = new[] { "Worker", "Merchant", "Socialite", "Drifter" };
        foreach (var role in roles)
        {
            var npc = assignments.FirstOrDefault(a => a.Role.ToString() == role);
            if (npc != null && ids.Count < count)
                ids.Add(npc.NpcId);
        }
        // Fill remaining with random NPCs
        var rng = new Random(_seed + 999);
        while (ids.Count < count && ids.Count < assignments.Count)
        {
            int id = rng.Next(assignments.Count);
            if (!ids.Contains(id)) ids.Add(id);
        }
        return ids;
    }


    private static void WriteTraces(DesSimulation sim, List<NpcSchedule> schedules,
        List<int> tracedIds, DateTime simStart)
    {
        using var writer = new StreamWriter(_traceFile, false, System.Text.Encoding.UTF8);

        foreach (int npcId in tracedIds)
        {
            var npc = schedules.FirstOrDefault(s => s.NpcId == npcId);
            if (npc == null) continue;

            var npcTraces = sim.Traces.Where(t => t.NpcId == npcId).OrderBy(t => t.Start).ToList();
            var npcEncounters = sim.TraceEncounters.Where(t => t.NpcId == npcId).OrderBy(t => t.Time).ToList();

            // Group by day
            int maxDay = Math.Min(_days, 3); // Show first 3 days in traces
            for (int day = 1; day <= maxDay; day++)
            {
                var dayStart = simStart.AddDays(day - 1);
                var dayEnd = simStart.AddDays(day);

                var dayTraces = npcTraces.Where(t => t.Start < dayEnd && t.End > dayStart).ToList();
                if (dayTraces.Count == 0) continue;

                writer.WriteLine($"=== NPC #{npcId} ({npc.Role}, seed={npc.Seed}) — Day {day} ===");

                foreach (var trace in dayTraces)
                {
                    string startTime = trace.Start.ToString("HH:mm");
                    string endTime = trace.End.ToString("HH:mm");
                    writer.WriteLine(
                        $"[{startTime}] node_arrival → \"{trace.Storylet}\" @ loc#{trace.LocationId} ({trace.LocationType})");

                    if (trace.TravelMinutes > 0.1f)
                        writer.WriteLine($"  travel: {trace.TravelMinutes:F1} min");

                    // Show key props
                    if (trace.PropsSnapshot != null)
                    {
                        var keyProps = new[] { "fatigue", "hunger", "wealth" };
                        var propsStr = string.Join(", ",
                            keyProps.Where(p => trace.PropsSnapshot.ContainsKey(p))
                                .Select(p => $"{p}={trace.PropsSnapshot[p]:F2}"));
                        if (propsStr.Length > 0)
                            writer.WriteLine($"  props: {propsStr}");
                    }

                    // Show deltas
                    if (trace.Deltas != null && trace.Deltas.Count > 0)
                    {
                        var deltasStr = string.Join(", ",
                            trace.Deltas.Where(d => MathF.Abs(d.Value) > 0.001f)
                                .Select(d => $"{d.Key} {(d.Value >= 0 ? "+" : "")}{d.Value:F3}"));
                        if (deltasStr.Length > 0)
                            writer.WriteLine($"  post: {deltasStr}");
                    }

                    // Show encounters during this storylet
                    var storyletEncounters = npcEncounters
                        .Where(e => e.Time >= trace.Start && e.Time < trace.End)
                        .ToList();
                    foreach (var enc in storyletEncounters)
                    {
                        writer.WriteLine(
                            $"  ** encounter: NPC #{enc.OtherId} ({enc.OtherRole}) — " +
                            $"{enc.InteractionType} (trust {enc.TrustBefore:F2}→{enc.TrustAfter:F2})");
                    }
                }
                writer.WriteLine();
            }
        }
    }


    private static void WriteGraph(DesSimulation sim, List<NpcSchedule> schedules,
        SpatialModel spatial)
    {
        var nodes = new List<Dictionary<string, object>>();
        foreach (var npc in schedules)
        {
            var loc = spatial.GetLocation(npc.HomeLocationId);
            nodes.Add(new Dictionary<string, object>
            {
                ["id"] = npc.NpcId,
                ["seed"] = npc.Seed,
                ["role"] = npc.Role,
                ["home"] = npc.HomeLocationId,
                ["props"] = npc.Properties.ToDictionary(
                    kv => kv.Key,
                    kv => (object)Math.Round(kv.Value, 3))
            });
        }

        var relationships = new List<Dictionary<string, object>>();
        foreach (var (key, state) in sim.Relationships.AllRelationships)
        {
            int a = (int)(key >> 32);
            int b = (int)(key & 0xFFFFFFFF);
            string tier = RelationshipTracker.TierFromTrust((state.TrustAtoB + state.TrustBtoA) / 2f);
            relationships.Add(new Dictionary<string, object>
            {
                ["a"] = a,
                ["b"] = b,
                ["trust_ab"] = Math.Round(state.TrustAtoB, 3),
                ["trust_ba"] = Math.Round(state.TrustBtoA, 3),
                ["tier"] = tier,
                ["total_interactions"] = state.TotalInteractions,
                ["interactions_by_type"] = state.InteractionsByType.ToDictionary(
                    kv => kv.Key, kv => (object)kv.Value),
                ["first_interaction_day"] = state.FirstInteractionDay,
                ["last_interaction_day"] = state.LastInteractionDay
            });
        }

        var graph = new Dictionary<string, object>
        {
            ["snapshot_day"] = _days,
            ["nodes"] = nodes,
            ["relationships"] = relationships
        };

        var jsonOpts = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(_graphFile, JsonSerializer.Serialize(graph, jsonOpts));
    }


    private static (bool pass, List<string> warnings) CheckTargets(Dictionary<string, object> metrics)
    {
        var warnings = new List<string>();
        bool pass = true;

        if (string.IsNullOrEmpty(_targetsFile) || !File.Exists(_targetsFile))
        {
            // Try default location
            string defaultPath = Path.Combine(AppContext.BaseDirectory, "testbed_targets.json");
            if (!File.Exists(defaultPath))
            {
                // Try relative to working directory
                defaultPath = "testbed_targets.json";
                // Also try Testbed directory
                if (!File.Exists(defaultPath))
                    defaultPath = "Testbed/testbed_targets.json";
            }
            if (File.Exists(defaultPath))
                _targetsFile = defaultPath;
        }

        if (string.IsNullOrEmpty(_targetsFile) || !File.Exists(_targetsFile))
            return (true, warnings); // No targets = pass

        try
        {
            var targetsJson = File.ReadAllText(_targetsFile);
            using var doc = JsonDocument.Parse(targetsJson);
            var root = doc.RootElement;

            // Check routine_completion_rate
            CheckRange(metrics, "routine_completion_rate", root, warnings, ref pass);

            // Check graph metrics
            if (metrics.TryGetValue("graph", out var graphObj) && graphObj is Dictionary<string, object> graph)
            {
                if (root.TryGetProperty("largest_component_fraction", out var lcf))
                    CheckMinMax(graph, "largest_component_fraction", lcf, warnings, ref pass);
                if (root.TryGetProperty("clustering_coefficient", out var cc))
                    CheckMinMax(graph, "clustering_coefficient", cc, warnings, ref pass);
                if (root.TryGetProperty("degree_distribution_gini", out var gini))
                    CheckMinMax(graph, "degree_distribution_gini", gini, warnings, ref pass);
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"Error parsing targets: {ex.Message}");
        }

        return (pass, warnings);
    }


    private static void CheckRange(Dictionary<string, object> metrics, string key,
        JsonElement root, List<string> warnings, ref bool pass)
    {
        if (!root.TryGetProperty(key, out var target)) return;
        if (!metrics.TryGetValue(key, out var valueObj)) return;
        double value = Convert.ToDouble(valueObj);
        CheckMinMax(value, key, target, warnings, ref pass);
    }


    private static void CheckMinMax(Dictionary<string, object> dict, string key,
        JsonElement target, List<string> warnings, ref bool pass)
    {
        if (!dict.TryGetValue(key, out var valueObj)) return;
        double value = Convert.ToDouble(valueObj);
        CheckMinMax(value, key, target, warnings, ref pass);
    }


    private static void CheckMinMax(double value, string key,
        JsonElement target, List<string> warnings, ref bool pass)
    {
        if (target.TryGetProperty("min", out var minEl))
        {
            double min = minEl.GetDouble();
            if (value < min)
            {
                warnings.Add($"{key} {value:F3} below target minimum {min:F3}");
                pass = false;
            }
        }
        if (target.TryGetProperty("max", out var maxEl))
        {
            double max = maxEl.GetDouble();
            if (value > max)
            {
                warnings.Add($"{key} {value:F3} above target maximum {max:F3}");
                pass = false;
            }
        }
    }


    private static void PrintStats(ClusterDesc cluster, SpatialModel model,
        List<NpcAssignment> assignments, int fragmentCount)
    {
        Console.Error.WriteLine("\n========================================");
        Console.Error.WriteLine("        CLUSTER STATISTICS");
        Console.Error.WriteLine("========================================");
        Console.Error.WriteLine($"  Cluster:          \"{cluster.Name}\"");
        Console.Error.WriteLine($"  Position:         {cluster.Pos}");
        Console.Error.WriteLine($"  Size:             {cluster.Size}");
        Console.Error.WriteLine($"  Fragment count:   {fragmentCount}");
        Console.Error.WriteLine();

        Console.Error.WriteLine("--- Spatial Model ---");
        Console.Error.WriteLine($"  Total locations:  {model.Locations.Count}");
        Console.Error.WriteLine($"  Buildings:        {model.BuildingCount}");
        Console.Error.WriteLine($"  Shops:            {model.ShopCount}");
        Console.Error.WriteLine($"  Street points:    {model.StreetPointCount}");
        Console.Error.WriteLine($"  Routes:           {model.Routes.Count}");
        Console.Error.WriteLine();

        var byType = model.Locations.GroupBy(l => l.Type).OrderBy(g => g.Key);
        Console.Error.WriteLine("--- Locations by Type ---");
        foreach (var group in byType)
            Console.Error.WriteLine($"  {group.Key,-20} {group.Count(),6}");
        Console.Error.WriteLine();

        var byRole = assignments.GroupBy(a => a.Role).OrderBy(g => g.Key);
        Console.Error.WriteLine($"--- NPC Assignments ({assignments.Count} NPCs) ---");
        foreach (var group in byRole)
            Console.Error.WriteLine($"  {group.Key,-20} {group.Count(),6}");
        Console.Error.WriteLine("========================================");
    }
}
