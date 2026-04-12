using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using static engine.Logger;

namespace engine.tale.bake;

/// <summary>
/// Build-time bake step for a single TALE social-structure scenario.
///
/// Mirrors Mazu.AnimationCompiler in role and lifecycle: Chushi instantiates one
/// of these per scenario in the config, calls Compile() in parallel with the
/// others, and the result lands as a "sc-{hash}" file in nogame/generated/.
///
/// The same class is used by Phase D2's ScenarioLibrary as the on-demand fallback
/// when a baked file is missing at runtime — exact mirror of how
/// Model.BakeAnimations falls through to AnimationCollection.BakeAnimations when
/// TryLoadModelAnimationCollection fails. Output sink is the only difference:
/// Compile() writes to OutputDirectory; CompileInMemory() returns the Scenario
/// object directly.
///
/// The simulation runs against a *synthetic* SpatialModel and a *synthetic* NPC
/// pool — there is no real ClusterDesc dependency. That is the entire point of
/// pre-computing scenarios: the social structure is independent of any one
/// cluster's geometry, so it can be re-applied to whatever cluster the player
/// happens to enter.
/// </summary>
public class ScenarioCompiler
{
    public required string CategoryName;
    public required int Index;
    public required int NpcCount;
    public required int Seed;
    public int SimulationDays = 365;
    public required string OutputDirectory;

    private static readonly string[] Roles =
    {
        "worker", "merchant", "socialite", "drifter",
        "authority", "nightworker", "hustler", "reveler"
    };

    private static readonly float[] DefaultRoleWeights =
    {
        0.30f, 0.13f, 0.15f, 0.12f, 0.10f, 0.08f, 0.07f, 0.05f
    };


    /// <summary>
    /// Run the bake and write the result to OutputDirectory/sc-{hash}.
    /// </summary>
    public void Compile()
    {
        Trace($"ScenarioCompiler: baking {CategoryName}/{Index} (npcCount={NpcCount}, seed={Seed}).");
        var scenario = CompileInMemory();
        string fileName = ScenarioFileName.Of(CategoryName, Index, Seed);
        string filePath = Path.Combine(OutputDirectory, fileName);
        ScenarioExporter.WriteToFile(scenario, filePath);
        Trace($"ScenarioCompiler: wrote {filePath} ({scenario.NpcCount} npcs, {scenario.Groups.Count} groups, {scenario.Relationships.Count} relationships).");
    }


    /// <summary>
    /// Run the bake and return the in-memory Scenario without touching disk.
    /// Used by the Phase D2 runtime fallback path (mirrors how
    /// AnimationCollection.BakeAnimations is called from Model.BakeAnimations
    /// when no pre-baked file is available).
    /// </summary>
    public Scenario CompileInMemory()
    {
        var spatial = BuildSyntheticSpatialModel(NpcCount, Seed);
        var schedules = BuildSyntheticNpcs(NpcCount, spatial, Seed);

        var library = LoadStoryletLibrary();

        var sim = new DesSimulation();
        var simStart = new DateTime(2024, 1, 1, 0, 0, 0);
        sim.Initialize(spatial, schedules, library, new NullEventLogger(), simStart, Seed);
        sim.RunUntil(simStart.AddDays(SimulationDays));

        return ScenarioExporter.Build(CategoryName, Index, Seed, SimulationDays, sim);
    }


    /// <summary>
    /// Load the storylet library from the engine's resource path. Storylet
    /// definitions live next to the rest of the game data so the bake step
    /// reads from the same source the runtime does.
    /// </summary>
    private static StoryletLibrary LoadStoryletLibrary()
    {
        var library = new StoryletLibrary();
        string resourcePath = engine.GlobalSettings.Get("Engine.ResourcePath");
        if (string.IsNullOrEmpty(resourcePath))
        {
            Trace("ScenarioCompiler: Engine.ResourcePath not set; storylet library will be empty.");
            return library;
        }
        string talePath = Path.Combine(resourcePath, "tale");
        if (Directory.Exists(talePath))
        {
            library.LoadFromDirectory(talePath);
            Trace($"ScenarioCompiler: loaded {library.All.Count} storylet definitions from {talePath}.");
        }
        else
        {
            Trace($"ScenarioCompiler: tale directory not found at {talePath}; library will be empty.");
        }
        return library;
    }


    /// <summary>
    /// Build a synthetic spatial model with abstract location buckets sized for
    /// the requested NPC count. Locations are placed on a coarse grid so the
    /// DES has plausible Euclidean distances for travel-time computations.
    ///
    /// The exact layout is unimportant — what matters is that there are enough
    /// shared venues for NPCs to bump into each other and form trust edges.
    /// </summary>
    private static SpatialModel BuildSyntheticSpatialModel(int npcCount, int seed)
    {
        var model = new SpatialModel();
        var rng = new Random(seed ^ 0x5C4E2A1B);

        int homeCount = Math.Max(8, npcCount);                  // 1 home per NPC
        int officeCount = Math.Max(4, npcCount / 4);
        int warehouseCount = Math.Max(2, npcCount / 8);
        int shopCount = Math.Max(2, npcCount / 10);
        int socialVenueCount = Math.Max(4, npcCount / 8);
        int streetCount = Math.Max(8, npcCount / 4);

        int locId = 0;

        // Home cluster
        for (int i = 0; i < homeCount; i++)
        {
            model.Locations.Add(new Location
            {
                Id = locId++,
                Type = "home",
                Position = RandomGridPosition(rng, 0f, 0f, 400f),
                Capacity = 4,
                QuarterIndex = 0,
                EstateIndex = i
            });
        }
        // Workplaces
        for (int i = 0; i < officeCount; i++)
        {
            model.Locations.Add(new Location
            {
                Id = locId++,
                Type = "office",
                Position = RandomGridPosition(rng, 500f, 0f, 200f),
                Capacity = 16,
                QuarterIndex = 1,
                EstateIndex = i
            });
        }
        for (int i = 0; i < warehouseCount; i++)
        {
            model.Locations.Add(new Location
            {
                Id = locId++,
                Type = "warehouse",
                Position = RandomGridPosition(rng, 500f, 300f, 200f),
                Capacity = 12,
                QuarterIndex = 2,
                EstateIndex = i
            });
        }
        for (int i = 0; i < shopCount; i++)
        {
            model.Locations.Add(new Location
            {
                Id = locId++,
                Type = "shop",
                Position = RandomGridPosition(rng, 250f, 250f, 200f),
                Capacity = 8,
                ShopType = "Game2",
                QuarterIndex = 3,
                EstateIndex = i
            });
        }
        // Social venues
        for (int i = 0; i < socialVenueCount; i++)
        {
            model.Locations.Add(new Location
            {
                Id = locId++,
                Type = "social_venue",
                Position = RandomGridPosition(rng, 300f, -200f, 200f),
                Capacity = 20,
                ShopType = (i % 2 == 0) ? "Drink" : "Eat",
                QuarterIndex = 4,
                EstateIndex = i
            });
        }
        // Streets
        for (int i = 0; i < streetCount; i++)
        {
            model.Locations.Add(new Location
            {
                Id = locId++,
                Type = "street_segment",
                Position = RandomGridPosition(rng, 0f, 0f, 600f),
                Capacity = 0,
                QuarterIndex = -1,
                EstateIndex = -1
            });
        }

        model.BuildIndex();
        Trace($"ScenarioCompiler: synthetic spatial model — {homeCount} homes, {officeCount} offices, {warehouseCount} warehouses, {shopCount} shops, {socialVenueCount} social, {streetCount} streets ({model.Locations.Count} total).");
        return model;
    }


    private static Vector3 RandomGridPosition(Random rng, float cx, float cz, float radius)
    {
        return new Vector3(
            cx + ((float)rng.NextDouble() * 2f - 1f) * radius,
            2.15f,
            cz + ((float)rng.NextDouble() * 2f - 1f) * radius);
    }


    /// <summary>
    /// Build a synthetic population for the bake. Roles are sampled from the
    /// same default distribution as TalePopulationGenerator (intentionally not
    /// reusing that class because it is wired to ClusterDesc / spatial cluster
    /// extraction). Properties follow the same per-role formulas so the bake
    /// produces NPCs that are statistically indistinguishable from runtime ones.
    /// </summary>
    private static List<NpcSchedule> BuildSyntheticNpcs(int npcCount, SpatialModel spatial, int seed)
    {
        var rng = new Random(seed);
        var schedules = new List<NpcSchedule>(npcCount);

        // Pre-bucket locations by type for fast role-aware assignment.
        var byType = new Dictionary<string, List<int>>();
        foreach (var loc in spatial.Locations)
        {
            if (!byType.TryGetValue(loc.Type, out var list))
            {
                list = new List<int>();
                byType[loc.Type] = list;
            }
            list.Add(loc.Id);
        }

        // We use a synthetic clusterIndex of 0 since the scenario is
        // cluster-independent. NPC IDs are still unique within this scenario.
        const int syntheticClusterIndex = 0;

        for (int i = 0; i < npcCount; i++)
        {
            string role = PickRole(rng);
            int homeId = PickLocationByRole(rng, byType, role, "home");
            int workId = PickLocationByRole(rng, byType, role, "workplace");
            int venueId = PickLocationByRole(rng, byType, role, "social_venue");

            var homeLoc = spatial.GetLocation(homeId);
            var workLoc = spatial.GetLocation(workId);

            int npcId = NpcSchedule.MakeNpcId(syntheticClusterIndex, i);

            schedules.Add(new NpcSchedule
            {
                NpcId = npcId,
                Seed = npcId,
                Role = role,
                ClusterIndex = syntheticClusterIndex,
                NpcIndex = i,
                HomeLocationId = homeId,
                WorkplaceLocationId = workId,
                SocialVenueIds = new List<int> { venueId },
                HomePosition = homeLoc?.Position ?? Vector3.Zero,
                WorkplacePosition = workLoc?.Position ?? Vector3.Zero,
                CurrentLocationId = homeId,
                CurrentWorldPosition = homeLoc?.Position ?? Vector3.Zero,
                Properties = GenerateProperties(rng, role),
                Trust = new Dictionary<int, float>(),
                HasPlayerDeviation = false,
                RoutingPreferences = new RoutingPreferences()
            });
        }

        return schedules;
    }


    private static string PickRole(Random rng)
    {
        float total = 0f;
        for (int i = 0; i < DefaultRoleWeights.Length; i++) total += DefaultRoleWeights[i];
        float roll = (float)rng.NextDouble() * total;
        float cum = 0f;
        for (int i = 0; i < DefaultRoleWeights.Length; i++)
        {
            cum += DefaultRoleWeights[i];
            if (roll < cum) return Roles[i];
        }
        return Roles[0];
    }


    private static int PickLocationByRole(
        Random rng, Dictionary<string, List<int>> byType, string role, string preferredType)
    {
        // Map preferred type to actual location buckets per role. Mirrors the
        // logic in TalePopulationGenerator.AssignLocationByRole, kept simple.
        List<string> bucketOrder = preferredType switch
        {
            "home" => new List<string> { "home", "street_segment" },
            "workplace" => role switch
            {
                "merchant" => new List<string> { "shop", "social_venue", "street_segment" },
                "socialite" or "drifter" or "hustler" => new List<string> { "street_segment", "social_venue" },
                "reveler" => new List<string> { "social_venue", "street_segment" },
                _ => new List<string> { "office", "warehouse", "street_segment" }
            },
            "social_venue" => new List<string> { "social_venue", "street_segment" },
            _ => new List<string> { preferredType }
        };

        foreach (var bucket in bucketOrder)
        {
            if (byType.TryGetValue(bucket, out var list) && list.Count > 0)
                return list[rng.Next(list.Count)];
        }
        // Total fallback: any location
        foreach (var list in byType.Values)
        {
            if (list.Count > 0) return list[rng.Next(list.Count)];
        }
        return 0;
    }


    /// <summary>
    /// Per-role property generation. Mirrors TalePopulationGenerator.GenerateProperties
    /// (intentional duplication: that method is private and tied to RandomSource;
    /// we want a System.Random-driven equivalent here so the bake stays self-contained).
    /// Any tuning change should be applied to both call sites.
    /// </summary>
    private static Dictionary<string, float> GenerateProperties(Random rng, string role)
    {
        var props = new Dictionary<string, float>
        {
            { "hunger", 0f },
            { "health", 0.9f + (float)rng.NextDouble() * 0.1f },
            { "fatigue", (float)rng.NextDouble() * 0.2f },
            { "anger", (float)rng.NextDouble() * 0.1f },
            { "fear", 0f },
            { "trust", 0.4f + (float)rng.NextDouble() * 0.2f },
            { "happiness", 0.4f + (float)rng.NextDouble() * 0.3f },
            { "reputation", 0.4f + (float)rng.NextDouble() * 0.2f }
        };
        switch (role)
        {
            case "worker":
                props["morality"] = 0.6f + (float)rng.NextDouble() * 0.2f;
                props["wealth"] = 0.3f + (float)rng.NextDouble() * 0.3f;
                break;
            case "merchant":
                props["morality"] = 0.5f + (float)rng.NextDouble() * 0.3f;
                props["wealth"] = 0.5f + (float)rng.NextDouble() * 0.3f;
                break;
            case "socialite":
                props["morality"] = 0.5f + (float)rng.NextDouble() * 0.3f;
                props["wealth"] = 0.4f + (float)rng.NextDouble() * 0.4f;
                break;
            case "drifter":
                props["morality"] = 0.3f + (float)rng.NextDouble() * 0.4f;
                props["wealth"] = 0.05f + (float)rng.NextDouble() * 0.2f;
                break;
            case "authority":
                props["morality"] = 0.6f + (float)rng.NextDouble() * 0.3f;
                props["wealth"] = 0.4f + (float)rng.NextDouble() * 0.2f;
                break;
            case "nightworker":
                props["morality"] = 0.5f + (float)rng.NextDouble() * 0.3f;
                props["wealth"] = 0.2f + (float)rng.NextDouble() * 0.3f;
                break;
            case "hustler":
                props["morality"] = 0.2f + (float)rng.NextDouble() * 0.4f;
                props["wealth"] = 0.2f + (float)rng.NextDouble() * 0.4f;
                break;
            case "reveler":
                props["morality"] = 0.4f + (float)rng.NextDouble() * 0.3f;
                props["wealth"] = 0.3f + (float)rng.NextDouble() * 0.4f;
                break;
            default:
                props["morality"] = 0.6f + (float)rng.NextDouble() * 0.2f;
                props["wealth"] = 0.3f + (float)rng.NextDouble() * 0.4f;
                break;
        }
        return props;
    }
}
