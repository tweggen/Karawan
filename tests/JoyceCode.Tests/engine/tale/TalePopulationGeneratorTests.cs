using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using engine.tale;
using Xunit;

namespace JoyceCode.Tests.engine.tale;

/// <summary>
/// Seed-based population (Phase 6 scripts 01–07, 10, 14–16, moved here in
/// KAR-468): the generator through its PopulationInput seam, so no generated
/// city is needed, and TaleManager's populate on top of it.
/// </summary>
public class TalePopulationGeneratorTests
{
    private static readonly DateTime T0 = new(2024, 1, 1, 8, 0, 0);
    private static readonly HashSet<string> Roles = new()
        { "worker", "merchant", "socialite", "drifter", "authority", "nightworker", "hustler", "reveler" };

    private static TalePopulationGenerator.PopulationInput Input(int cluster = 7, int streetPoints = 10, float downtown = 0.3f)
    {
        var points = Enumerable.Range(0, streetPoints)
            .Select(i => new Vector3(100 + 10 * i, 2.15f, 50 - 7 * i)).ToList();
        return new TalePopulationGenerator.PopulationInput(
            cluster, $"cluster-clusters-test-{cluster}", points, downtown, new Vector3(100, 0, 50));
    }

    private static string Json(IEnumerable<NpcSchedule> schedules) => NpcScheduleJson.WriteDeviations(schedules);


    [Fact]
    public void SameInput_SamePopulation()   // 01
    {
        var gen = new TalePopulationGenerator();
        Assert.Equal(Json(gen.GenerateFrom(Input(), null)), Json(gen.GenerateFrom(Input(), null)));
        Assert.NotEqual(Json(gen.GenerateFrom(Input(), null)), Json(gen.GenerateFrom(Input(cluster: 8), null)));
    }


    [Theory]   // 02, 16
    [InlineData(0, 0)]
    [InlineData(1, 3)]
    [InlineData(10, 30)]
    [InlineData(2000, 4095)]
    public void Count_IsThreePerStreetPoint_CappedAt4095(int streetPoints, int expected)
    {
        Assert.Equal(expected, new TalePopulationGenerator().GenerateFrom(Input(streetPoints: streetPoints), null).Count);
    }


    [Fact]
    public void SkippingAnIndex_LeavesEveryOtherNpcUnchanged()   // 03
    {
        var gen = new TalePopulationGenerator();
        var all = gen.GenerateFrom(Input(), null).ToDictionary(n => n.NpcIndex);
        var skipped = gen.GenerateFrom(Input(), null, new HashSet<int> { 5, 6 });

        Assert.Equal(all.Count - 2, skipped.Count);
        Assert.DoesNotContain(skipped, n => n.NpcIndex is 5 or 6);
        foreach (var npc in skipped)
            Assert.Equal(Json(new[] { all[npc.NpcIndex] }), Json(new[] { npc }));
    }


    [Fact]
    public void Roles_AreValid_AndDowntownShiftsTowardsMerchants()   // 04
    {
        var gen = new TalePopulationGenerator();
        var quiet = gen.GenerateFrom(Input(streetPoints: 1000, downtown: 0f), null);
        var downtown = gen.GenerateFrom(Input(streetPoints: 1000, downtown: 1f), null);

        Assert.All(quiet.Concat(downtown), n => Assert.Contains(n.Role, Roles));
        Assert.True(downtown.Count(n => n.Role == "merchant") > quiet.Count(n => n.Role == "merchant"));
        Assert.True(downtown.Count(n => n.Role == "drifter") < quiet.Count(n => n.Role == "drifter"));
    }


    [Fact]
    public void WithoutLocations_HomeAndWorkAreStreetPoints()   // 05, 06
    {
        var input = Input();
        var points = new HashSet<Vector3>(input.StreetPoints);
        foreach (var npc in new TalePopulationGenerator().GenerateFrom(input, null))
        {
            Assert.Contains(npc.HomePosition, points);
            Assert.Contains(npc.WorkplacePosition, points);
            Assert.Equal(input.StreetPoints[npc.HomeLocationId], npc.HomePosition);
        }
    }


    [Fact]
    public void WithLocations_HomeAndWorkAreLocationsOfTheModel()   // 05, 06
    {
        var model = new SpatialModel();
        model.Locations.Add(new Location { Id = 1, Type = "home", Position = new Vector3(10, 0, 10), EntryPosition = new Vector3(11, 0, 10) });
        model.Locations.Add(new Location { Id = 2, Type = "workplace", Position = new Vector3(40, 0, 10), EntryPosition = new Vector3(41, 0, 10) });
        model.Locations.Add(new Location { Id = 3, Type = "social_venue", Position = new Vector3(20, 0, 30), EntryPosition = new Vector3(21, 0, 30) });
        model.BuildIndex();

        foreach (var npc in new TalePopulationGenerator().GenerateFrom(Input(), model))
        {
            Assert.NotNull(model.GetLocation(npc.HomeLocationId));
            Assert.NotNull(model.GetLocation(npc.WorkplaceLocationId));
            Assert.Equal(model.GetLocation(npc.HomeLocationId).Position, npc.HomePosition);
            Assert.All(npc.SocialVenueIds, v => Assert.NotNull(model.GetLocation(v)));
        }
    }


    [Fact]
    public void Properties_AreInitialisedInRange_AndIdsEncodeClusterAndIndex()   // 07, 08
    {
        foreach (var npc in new TalePopulationGenerator().GenerateFrom(Input(cluster: 9), null))
        {
            Assert.NotEmpty(npc.Properties);
            Assert.All(npc.Properties.Values, v => Assert.InRange(v, 0f, 1f));
            Assert.False(npc.HasPlayerDeviation);
            Assert.Equal(NpcSchedule.MakeNpcId(9, npc.NpcIndex), npc.NpcId);
            Assert.Equal(9, NpcSchedule.GetClusterIndex(npc.NpcId));
            Assert.Equal(npc.NpcIndex, NpcSchedule.GetNpcIndex(npc.NpcId));
        }
    }


    private static TaleManager Manager()
    {
        var dir = Path.Combine(Path.GetTempPath(), "tale-468-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var library = new StoryletLibrary();
        try
        {
            File.WriteAllText(Path.Combine(dir, "test.json"), """
            [ { "id": "idle", "roles": ["worker","merchant","socialite","drifter","authority","nightworker","hustler","reveler"],
                "duration_minutes": 30, "location": "current" },
              { "id": "wander", "roles": ["fallback_only"], "duration_minutes": 30, "location": "current" },
              { "id": "rest", "roles": ["fallback_only"], "duration_minutes": 60, "location": "current" } ]
            """);
            library.LoadFromDirectory(dir);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
        var tm = new TaleManager();
        tm.Initialize(library);
        return tm;
    }

    private static void Populate(TaleManager tm, int cluster) =>
        tm.PopulateClusterWith(cluster, null, T0, skip => new TalePopulationGenerator().GenerateFrom(Input(cluster), null, skip));


    [Fact]
    public void PopulatingTwice_IsANoOp()   // 10
    {
        var tm = Manager();
        Populate(tm, 7);
        var first = tm.GetNpcsInCluster(7);
        Populate(tm, 7);
        var second = tm.GetNpcsInCluster(7);

        Assert.Equal(first.Count, second.Count);
        Assert.All(first.Zip(second), p => Assert.Same(p.First, p.Second));
    }


    [Fact]
    public void DepopulateThenRepopulate_GivesTheSamePrimaryNpcs()   // 14
    {
        var tm = Manager();
        Populate(tm, 7);
        string before = Json(tm.GetNpcsInCluster(7));
        tm.DepopulateCluster(7);
        Assert.Empty(tm.GetNpcsInCluster(7));
        Populate(tm, 7);

        Assert.Equal(before, Json(tm.GetNpcsInCluster(7)));
    }


    [Fact]
    public void TwoClusters_AreIndependent_WithDisjointIds()   // 15
    {
        var tm = Manager();
        Populate(tm, 7);
        Populate(tm, 8);
        var a = tm.GetNpcsInCluster(7);
        var b = tm.GetNpcsInCluster(8);

        Assert.Equal(30, a.Count);
        Assert.Equal(30, b.Count);
        Assert.Empty(a.Select(n => n.NpcId).Intersect(b.Select(n => n.NpcId)));
        tm.DepopulateCluster(7);
        Assert.Equal(30, tm.GetNpcsInCluster(8).Count);
    }
}
