using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using builtin.tools;
using engine.tale;
using engine.world;
using engine.world.components;
using JoyceCode.Tests.engine.streets;
using JoyceCode.Tests.engine.tale.bake;
using nogame.cities;
using Xunit;
using Xunit.Abstractions;

namespace JoyceCode.Tests.engine.tale;


/**
 * KAR-497 (TALE S2-b): gossip at the encounter sites. The DES (the start city's real
 * population, the shipped storylets) passes a punched victim's opinion on to NPCs who never
 * met the player; without any player entry nothing is heard at all.
 */
public class GossipWiringTests : IClassFixture<ScenarioCompilerTests.RegistryFixture>
{
    private readonly ITestOutputHelper _out;
    public GossipWiringTests(ScenarioCompilerTests.RegistryFixture _, ITestOutputHelper output) => _out = output;

    private static readonly DateTime T0 = new(2000, 1, 2, 6, 0, 0);

    private static readonly object _lo = new();
    private static (SpatialModel Model, Func<List<NpcSchedule>> Npcs)? _city;


    private static (SpatialModel Model, Func<List<NpcSchedule>> Npcs) _yelukhdidru()
    {
        lock (_lo)
        {
            if (_city != null) return _city.Value;
            new GenerateClustersOperator("mydear")._generateClusterList(null, out var clusters);
            var cd = clusters.Single(x => x.Name == "Yelukhdidru");
            var (store, _) = StreetHarness.GenerateHeavyFirstReporting(cd.IdString, cd.Size,
                sp => ShippedTerrain.HeightAt(cd.Pos.X + sp.Pos.X, cd.Pos.Z + sp.Pos.Y));
            cd.StreetHeightSource = ShippedTerrain.StreetHeightsOf(cd, store);
            var quarters = StreetHarness.GenerateQuarters(cd, store, cd.IdString);
            var rnd = new RandomSource(cd.Name);
            ShopPlacement.Place(cd, quarters, rnd, 100f,
                v => cd.GetAttributeIntensity(v, ClusterDesc.LocationAttributes.Shopping), MapIcon.IconCode.Game2);
            ShopPlacement.Place(cd, quarters, rnd, 100f,
                v => cd.GetAttributeIntensity(v, ClusterDesc.LocationAttributes.Downtown), MapIcon.IconCode.Drink);
            ShopPlacement.Place(cd, quarters, rnd, 100f, v => 1f, MapIcon.IconCode.Eat);
            var model = SpatialModel.ExtractFrom(cd, quarters, store);
            var input = new TalePopulationGenerator.PopulationInput(cd.Index, cd.GetKey(),
                store.GetStreetPoints().Select(sp => sp.Pos3 + cd.Pos).ToList(),
                cd.GetAttributeIntensity(cd.Pos, ClusterDesc.LocationAttributes.Downtown), cd.Pos);
            _city = (model, () => new TalePopulationGenerator().GenerateFrom(input, model));
            return _city.Value;
        }
    }


    private static StoryletLibrary _shipped()
    {
        string tale = Path.GetFullPath(Path.Combine(global::engine.GameRoot.PathTo("JoyceCode"), "..", "models", "tale"));
        var library = new StoryletLibrary();
        library.LoadFromDirectory(tale);
        return library;
    }


    [Fact]
    public void TheDesPassesAPunchedVictimsOpinionOnByGenerationAndDay()
    {
        var (model, generate) = _yelukhdidru();
        var npcs = generate();
        var victim = npcs.First(n => n.Role == "worker");
        victim.Trust ??= new Dictionary<int, float>();
        victim.Trust[PlayerTrust.Key] = 0f;                    // punched: regard -1
        victim.Properties["player_fact.hit_me"] = 1f;

        // Its friends: everyone sharing its home or workplace, trusted both ways.
        var friends = npcs.Where(n => n != victim &&
            (n.HomeLocationId == victim.HomeLocationId || n.WorkplaceLocationId == victim.WorkplaceLocationId)).ToList();
        Assert.NotEmpty(friends);

        var sim = new DesSimulation();
        sim.Initialize(model, npcs, _shipped(), new NullEventLogger(), T0);
        foreach (var f in friends)
        {
            var st = sim.Relationships.GetOrCreate(victim.NpcId, f.NpcId);
            st.TrustAtoB = st.TrustBtoA = 0.7f;
        }

        string Report(DateTime at)
        {
            var heard = sim.Npcs.Values.Where(n => n.Hearsay != null && n.Hearsay.IsPresentAt(at)).ToList();
            return $"{heard.Count} heard (" + string.Join(", ", heard.GroupBy(h => h.Hearsay.Generation)
                .OrderBy(g => g.Key).Select(g => $"gen {g.Key}: {g.Count()}")) + ")";
        }

        sim.RunUntil(T0.AddHours(23));
        var day1 = sim.Npcs.Values.Where(n => n.Hearsay != null).ToList();
        _out.WriteLine($"after 23 h: {Report(T0.AddHours(23))}; {friends.Count} friends seeded");
        Assert.NotEmpty(day1);
        Assert.All(day1, n => Assert.Equal(1, n.Hearsay.Generation));       // owner: one generation per day
        Assert.All(day1, n => Assert.True(n.Hearsay.Regard < 0f));
        Assert.Contains(day1, n => n.Hearsay.Has("hurt_someone"));

        sim.RunUntil(T0.AddDays(3));
        _out.WriteLine($"after 3 days: {Report(T0.AddDays(3))}");

        // Hearing is not meeting: nobody else came to know the player.
        Assert.Equal(new[] { victim.NpcId }, sim.Npcs.Values.Where(PlayerTrust.Knows).Select(n => n.NpcId));
    }


    [Fact]
    public void WithoutAnyPlayerEntryNobodyHearsAnything()
    {
        var (model, generate) = _yelukhdidru();
        var sim = new DesSimulation();
        sim.Initialize(model, generate(), _shipped(), new NullEventLogger(), T0);
        sim.RunUntil(T0.AddDays(2));
        Assert.All(sim.Npcs.Values, n => Assert.Null(n.Hearsay));
    }
}
