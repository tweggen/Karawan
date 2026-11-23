using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using builtin.tools;
using engine.streets;
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
 * KAR-488 (TALE S2b-c) measurements, not gates (CI excludes Category=Perf): Ufyo, 4095 NPCs,
 * the shipped storylets, one game day played as the game plays it - the background tick
 * every frame at 60 fps over a 30-minute game day, the social tick every 10 game minutes.
 * Reports TickEncounters with and without the shared encounter rules, the background-tick
 * slice, and the live claim rate against the DES's for the same NPCs.
 */
public class LiveSocialPerfTests : IClassFixture<ScenarioCompilerTests.RegistryFixture>
{
    private readonly ITestOutputHelper _output;
    public LiveSocialPerfTests(ScenarioCompilerTests.RegistryFixture _, ITestOutputHelper output) => _output = output;

    private static readonly DateTime T0 = new(2000, 1, 2, 6, 0, 0);


    internal static (ClusterDesc Cd, SpatialModel Model, Func<List<NpcSchedule>> Npcs) _ufyo()
    {
        new GenerateClustersOperator("mydear")._generateClusterList(null, out var clusters);
        var cd = clusters.Single(x => x.Name == "Ufyo");
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
        return (cd, model, () => new TalePopulationGenerator().GenerateFrom(input, model));
    }


    internal static StoryletLibrary _shipped()
    {
        string tale = Path.GetFullPath(Path.Combine(global::engine.GameRoot.PathTo("JoyceCode"), "..", "models", "tale"));
        var library = new StoryletLibrary();
        library.LoadFromDirectory(tale);
        return library;
    }


    private static string _pct(List<double> xs)
    {
        var s = xs.OrderBy(x => x).ToArray();
        if (s.Length == 0) return "n/a";
        return $"mean {s.Average():F3} ms, p99 {s[(int)(s.Length * 0.99)]:F3}, p99.9 {s[Math.Min(s.Length - 1, (int)(s.Length * 0.999))]:F3}, max {s[^1]:F3} (n={s.Length})";
    }


    private static void _scan(InteractionPool pool, Dictionary<int, (string Type, int? Claimer)> seen)
    {
        foreach (var r in pool.Requests.Values)
            seen[r.Id] = (r.Type, r.ClaimerId ?? (seen.TryGetValue(r.Id, out var o) ? o.Claimer : null));
    }


    private static string _rate(Dictionary<int, (string Type, int? Claimer)> seen)
    {
        int claimed = seen.Values.Count(v => v.Claimer.HasValue);
        var byType = seen.Values.GroupBy(v => v.Type).OrderBy(g => g.Key)
            .Select(g => $"{g.Key} {g.Count(v => v.Claimer.HasValue)}/{g.Count()}");
        return $"{claimed}/{seen.Count} claimed ({(seen.Count > 0 ? 100.0 * claimed / seen.Count : 0):F0} %): {string.Join(", ", byType)}";
    }


    [Theory]
    [Trait("Category", "Perf")]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void OneGameDayInUfyo(bool liveRules, bool encounterRules)
    {
        var (cd, model, npcs) = _ufyo();
        var library = _shipped();

        var tm = new TaleManager();
        tm.Initialize(library);
        tm.PopulateClusterWith(cd.Index, model, T0, _ => npcs());
        Assert.Equal(4095, tm.GetNpcsInCluster(cd.Index).Count);
        var pool = tm.GetRequestPool(cd.Index);
        tm.LiveSocialRules = liveRules;
        int poolMax = 0; long poolSum = 0;

        var evolver = new RuntimeSocialEvolver();
        const int frames = 30 * 60 * 60;
        var frameStep = TimeSpan.FromDays(1) / frames;
        var slice = new List<double>(frames);
        var social = new List<double>();
        var encounters = new List<double>();
        var seen = new Dictionary<int, (string, int?)>();
        int met = 0;
        DateTime now = T0, lastSocial = T0;
        var sw = new Stopwatch();
        evolver.TickEncounters(cd.Index, tm.GetNpcsInCluster(cd.Index), model, now);

        for (int f = 0; f < frames; f++)
        {
            now += frameStep;
            sw.Restart();
            tm.AdvanceAbstractSchedules(now, 512, 16);
            tm.TickLiveSocial(now);
            sw.Stop();
            slice.Add(sw.Elapsed.TotalMilliseconds);

            if ((now - lastSocial).TotalMinutes < 10) continue;
            lastSocial = now;
            tm.CatchUpCluster(cd.Index, now);
            var list = tm.GetNpcsInCluster(cd.Index);
            sw.Restart();
            var st = evolver.TickEncounters(cd.Index, list, model, now,
                encounterRules ? (a, b, t) => tm.OnLiveEncounter(cd.Index, a, b, t) : null);
            sw.Stop();
            encounters.Add(sw.Elapsed.TotalMilliseconds);
            met += st.Encounters;
            _scan(pool, seen);
            poolMax = Math.Max(poolMax, pool.Requests.Count);
            poolSum += pool.Requests.Count;
        }

        _output.WriteLine($"arrival rules {(liveRules ? "ON" : "OFF")}, encounter rules {(encounterRules ? "ON" : "OFF")}: " +
                          $"{met} encounters; pool mean {poolSum / Math.Max(1, encounters.Count)}, max {poolMax}");
        _output.WriteLine($"  TickEncounters: {_pct(encounters)}");
        _output.WriteLine($"  background slice (+TickLiveSocial): {_pct(slice)}");
        _output.WriteLine($"  live requests: {_rate(seen)}");

        if (encounterRules)
        {
            // The DES over the same NPCs and the same day, scanned at the same cadence.
            var sim = new DesSimulation();
            sim.Initialize(model, npcs(), library, new NullEventLogger(), T0);
            var desSeen = new Dictionary<int, (string, int?)>();
            for (var t = T0.AddMinutes(10); t <= T0.AddDays(1); t = t.AddMinutes(10))
            {
                sim.RunUntil(t);
                _scan(sim.InteractionPool, desSeen);
            }
            _output.WriteLine($"  DES requests:  {_rate(desSeen)}");
        }
        Assert.True(met > 0);
    }
}
