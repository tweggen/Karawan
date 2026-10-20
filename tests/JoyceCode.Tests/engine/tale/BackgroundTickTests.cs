using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using engine.tale;
using engine.tale.bake;
using Xunit;
using Xunit.Abstractions;

namespace JoyceCode.Tests.engine.tale;

/// <summary>
/// TALE S0 W5 (KAR-450): the background tick advances the schedules no
/// entity runs (Tier 3), at event time, so abstract NPCs follow their day and
/// the result does not depend on how often the tick runs.
/// </summary>
public class BackgroundTickTests
{
    private const int Cluster = 6;
    private static readonly DateTime Morning = new(2000, 1, 2, 6, 0, 0);

    private readonly ITestOutputHelper _output;

    public BackgroundTickTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /*
     * A working day: home at night, morning and evening, the workplace from
     * eight to five. "flash" storylets are a minute long, so with the -5 min
     * jitter they can end before they start.
     */
    private const string StoryletsJson = """
    [
      { "id": "sleep",   "roles": ["tester"], "location": "home",      "duration_minutes": 480,
        "time_of_day": { "min": "22:00", "max": "05:00" } },
      { "id": "morning", "roles": ["tester"], "location": "home",      "duration_minutes": 30,
        "time_of_day": { "min": "05:00", "max": "08:00" } },
      { "id": "work",    "roles": ["tester"], "location": "workplace", "duration_minutes": 120,
        "time_of_day": { "min": "08:00", "max": "17:00" } },
      { "id": "evening", "roles": ["tester"], "location": "home",      "duration_minutes": 60,
        "time_of_day": { "min": "17:00", "max": "22:00" } },
      { "id": "flash",   "roles": ["flasher"], "location": "current",  "duration_minutes": 1 },
      { "id": "wander",  "roles": ["fallback_only"], "duration_minutes": 30, "location": "current" },
      { "id": "rest",    "roles": ["fallback_only"], "duration_minutes": 60, "location": "current" }
    ]
    """;

    private const int Home = 1;
    private const int Work = 2;

    private static StoryletLibrary MakeLibrary()
    {
        // LoadFromString builds no id index; the directory loader does.
        var dir = Path.Combine(Path.GetTempPath(), "tale-w5-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "test.json"), StoryletsJson);
            var library = new StoryletLibrary();
            library.LoadFromDirectory(dir);
            return library;
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static SpatialModel MakeModel()
    {
        var model = new SpatialModel();
        model.Locations.Add(new Location { Id = Home, Type = "home", Position = new Vector3(0, 0, 0) });
        model.Locations.Add(new Location { Id = Work, Type = "workplace", Position = new Vector3(600, 0, 0) });
        model.BuildIndex();
        return model;
    }

    private static Func<HashSet<int>, List<NpcSchedule>> Generator(int count, string role) => skip =>
    {
        var list = new List<NpcSchedule>();
        for (int i = 0; i < count; i++)
        {
            if (skip != null && skip.Contains(i)) continue;
            list.Add(new NpcSchedule
            {
                NpcId = NpcSchedule.MakeNpcId(Cluster, i), NpcIndex = i, ClusterIndex = Cluster,
                Seed = i, Role = role,
                HomeLocationId = Home, WorkplaceLocationId = Work, CurrentLocationId = Home,
                Properties = new Dictionary<string, float>(),
                Trust = new Dictionary<int, float>(),
            });
        }
        return list;
    };

    private static TaleManager MakePopulated(StoryletLibrary library, int count, DateTime at, string role = "tester")
    {
        var tm = new TaleManager();
        tm.Initialize(library);
        tm.PopulateClusterWith(Cluster, MakeModel(), at, Generator(count, role),
            _ => Array.Empty<ScenarioApplicator.AppliedGroup>());
        return tm;
    }

    private static string Describe(NpcSchedule s) =>
        $"{s.CurrentStorylet}@{s.CurrentLocationId} {s.CurrentStart:O}-{s.CurrentEnd:O} step={s.ScheduleStep}";

    private static string DescribeAll(TaleManager tm) =>
        string.Join("\n", tm.GetNpcsInCluster(Cluster).Select(Describe));

    private static double ShareAt(TaleManager tm, int locationId) =>
        tm.GetNpcsInCluster(Cluster).Count(n => n.CurrentLocationId == locationId)
        / (double)tm.GetNpcsInCluster(Cluster).Count;


    [Fact]
    public void LocationDistributionChangesOverOneGameDay()
    {
        var tm = MakePopulated(MakeLibrary(), 40, Morning);
        var visited = tm.GetNpcsInCluster(Cluster).ToDictionary(n => n.NpcId, n => new HashSet<int> { n.CurrentLocationId });

        var shares = new Dictionary<int, (double Home, double Work)>
        {
            [6] = (ShareAt(tm, Home), ShareAt(tm, Work))
        };
        for (var now = Morning.AddMinutes(10); now <= Morning.AddHours(24); now = now.AddMinutes(10))
        {
            tm.AdvanceAbstractSchedules(now, int.MaxValue, int.MaxValue);
            foreach (var npc in tm.GetNpcsInCluster(Cluster)) visited[npc.NpcId].Add(npc.CurrentLocationId);
            if (now.Minute == 0 && (now.Hour == 12 || now.Hour == 20))
                shares[now.Hour] = (ShareAt(tm, Home), ShareAt(tm, Work));
        }

        _output.WriteLine(string.Join(", ", shares.Select(kv => $"{kv.Key}:00 home={kv.Value.Home:F2} work={kv.Value.Work:F2}")));
        Assert.True(shares[6].Home >= 0.8, $"06:00 home share {shares[6].Home}");
        Assert.True(shares[12].Work >= 0.8, $"12:00 work share {shares[12].Work}");
        Assert.True(shares[20].Home >= 0.8, $"20:00 home share {shares[20].Home}");
        Assert.All(visited.Values, v => Assert.True(v.Count >= 2));
    }


    [Fact]
    public void CatchUpIsCadenceIndependent()
    {
        var library = MakeLibrary();
        var fine = MakePopulated(library, 20, Morning);
        var coarse = MakePopulated(library, 20, Morning);
        Assert.Equal(DescribeAll(fine), DescribeAll(coarse));

        for (int minute = 1; minute <= 24 * 60; minute++)
        {
            var now = Morning.AddMinutes(minute);
            fine.AdvanceAbstractSchedules(now, int.MaxValue, int.MaxValue);
            if (minute % 60 == 0)
            {
                coarse.AdvanceAbstractSchedules(now, int.MaxValue, int.MaxValue);
                Assert.Equal(DescribeAll(fine), DescribeAll(coarse));
            }
        }
    }


    [Fact]
    public void MaterialisedAndTier2AreNotAdvanced_ThenCatchUp()
    {
        var tm = MakePopulated(MakeLibrary(), 3, Morning);
        int materialised = NpcSchedule.MakeNpcId(Cluster, 0);
        int other = NpcSchedule.MakeNpcId(Cluster, 1);
        tm.SetMaterialized(materialised);
        string before = Describe(tm.GetSchedule(materialised));
        string otherBefore = Describe(tm.GetSchedule(other));

        var noon = Morning.AddHours(6);
        var stats = tm.AdvanceAbstractSchedules(noon, int.MaxValue, int.MaxValue);

        Assert.Equal(before, Describe(tm.GetSchedule(materialised)));
        Assert.NotEqual(otherBefore, Describe(tm.GetSchedule(other)));
        Assert.True(stats.SkippedMaterialised >= 1);

        tm.SetDematerialized(materialised);
        tm.CatchUpCluster(Cluster, noon);
        var caughtUp = tm.GetSchedule(materialised);
        Assert.True(caughtUp.CurrentEnd > noon);
        Assert.Equal(Work, caughtUp.CurrentLocationId);
    }


    [Fact]
    public void NoInfiniteLoopOnShortStorylets()
    {
        var tm = MakePopulated(MakeLibrary(), 5, Morning, role: "flasher");

        var now = Morning.AddHours(6);
        var stats = tm.AdvanceAbstractSchedules(now, int.MaxValue, int.MaxValue);

        // 360 one-minute events in six hours: each NPC hits the cap and resyncs.
        Assert.Equal(5, stats.Capped);
        Assert.True(stats.Steps <= 5 * (TaleManager.MaxCatchUpSteps + 1));

        // A second slice at the same time does not loop either.
        var again = tm.AdvanceAbstractSchedules(now, int.MaxValue, int.MaxValue);
        Assert.True(again.Steps <= 5);
    }


    [Fact]
    public void LargeGapResyncs()
    {
        var tm = MakePopulated(MakeLibrary(), 5, Morning);

        var later = Morning.AddDays(3).AddHours(6);   // 12:00 three days on
        var stats = tm.AdvanceAbstractSchedules(later, int.MaxValue, int.MaxValue);

        Assert.Equal(5, stats.Resynced);
        Assert.Equal(5, stats.Steps);
        Assert.All(tm.GetNpcsInCluster(Cluster), n => Assert.Equal(later, n.LastAdvanceTime));
    }


    [Fact]
    public void BudgetsSliceTheWork()
    {
        var tm = MakePopulated(MakeLibrary(), 100, Morning);
        var noon = Morning.AddHours(6);

        var first = tm.AdvanceAbstractSchedules(noon, scanBudget: 30, advanceBudget: 1000);
        Assert.Equal(30, first.Scanned);

        var byAdvance = tm.AdvanceAbstractSchedules(noon, scanBudget: 1000, advanceBudget: 10);
        Assert.Equal(10, byAdvance.Advanced);

        // The cursor moves on: enough slices reach every NPC.
        for (int i = 0; i < 20; i++) tm.AdvanceAbstractSchedules(noon, 30, 1000);
        Assert.All(tm.GetNpcsInCluster(Cluster), n => Assert.True(n.CurrentEnd > noon));
    }


    /*
     * Not a gate (CI excludes Category=Perf): one game day at 4095 NPCs, a
     * 30-minute game day at 60 frames per second, at the default advance
     * budget (16) and the plan's first guess (64). Calls over 1 ms are listed
     * with whether a gen-0 GC ran during them: measured, nearly all did.
     */
    [Theory]
    [Trait("Category", "Perf")]
    [InlineData(16)]
    [InlineData(64)]
    public void PerfOneGameDayAt4095Npcs(int advanceBudget)
    {
        var tm = MakePopulated(MakeLibrary(), 4095, Morning);
        const int frames = 30 * 60 * 60;
        var frameStep = TimeSpan.FromDays(1) / frames;

        var total = new AbstractTickStats();
        var perCall = new double[frames];
        var slow = new List<string>();
        var now = Morning;
        for (int f = 0; f < frames; f++)
        {
            now += frameStep;
            int gcBefore = GC.CollectionCount(0);
            var s = tm.AdvanceAbstractSchedules(now, 512, advanceBudget);
            total.Add(s);
            perCall[f] = s.ElapsedMs;
            if (s.ElapsedMs > 1.0)
            {
                slow.Add($"frame {f}: {s.ElapsedMs:F2} ms, advanced {s.Advanced}, " +
                         $"gc {(GC.CollectionCount(0) > gcBefore ? "yes" : "no")}");
            }
        }

        var sorted = perCall.OrderBy(x => x).ToArray();
        int over1Ms = perCall.Count(x => x > 1.0);
        int maxAt = Array.IndexOf(perCall, sorted[^1]);
        _output.WriteLine($"advance budget {advanceBudget}, {frames} frames: {total}; mean {total.ElapsedMs / frames:F4} ms, " +
                          $"p99 {sorted[(int)(frames * 0.99)]:F3} ms, p99.9 {sorted[(int)(frames * 0.999)]:F3} ms, " +
                          $"max {sorted[^1]:F3} ms (frame {maxAt}), calls over 1 ms: {over1Ms}" +
                          (slow.Count > 0 ? "\n  " + string.Join("\n  ", slow) : ""));
        Assert.True(total.Steps > 4095, "the day must actually be simulated");
    }
}
