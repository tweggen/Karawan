using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using engine.tale;
using Xunit;

namespace JoyceCode.Tests.engine.tale;

/// <summary>
/// TALE S0 W2 (KAR-446): AdvanceNpc must give an NPC the same schedule no
/// matter which other NPCs were advanced in between (it used to draw storylet
/// durations and street choices from one shared System.Random), and must put
/// CurrentWorldPosition where the NPC actually stands (the entry position),
/// not at the location's centre.
/// </summary>
public class AdvanceNpcDeterminismTests
{
    private const int Cluster = 3;
    private static readonly DateTime T0 = new(2000, 1, 2, 12, 0, 0);

    /*
     * One role whose only storylet goes to a random street with a duration
     * range, so both former _rng draws (duration and street) are exercised
     * on every step. The required fallbacks get a role of their own, or
     * they would be universal candidates for every NPC.
     */
    private const string StoryletsJson = """
    [
      { "id": "wander_test", "roles": ["tester"], "location": "random_street",
        "duration_minutes_min": 10, "duration_minutes_max": 90 },
      { "id": "wander", "roles": ["fallback_only"], "duration_minutes": 30, "location": "current" },
      { "id": "rest", "roles": ["fallback_only"], "duration_minutes": 60, "location": "current" }
    ]
    """;

    private static StoryletLibrary MakeLibrary()
    {
        var dir = Path.Combine(Path.GetTempPath(), "tale-w2-" + Guid.NewGuid().ToString("N"));
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
        model.Locations.Add(new Location { Id = 1, Type = "home", Position = new Vector3(0, 0, 0) });
        for (int i = 0; i < 12; i++)
        {
            var centre = new Vector3(100f + 37f * i, 0, 50f * (i % 3));
            model.Locations.Add(new Location
            {
                Id = 100 + i,
                Type = "street_segment",
                Position = centre,
                EntryCandidates = new List<Vector3>
                {
                    centre + new Vector3(6, 0, 6), centre + new Vector3(-6, 0, 6),
                    centre + new Vector3(6, 0, -6), centre + new Vector3(-6, 0, -6),
                }
            });
        }
        model.BuildIndex();
        return model;
    }

    private static NpcSchedule MakeNpc(int id) => new()
    {
        NpcId = id,
        Seed = id,
        Role = "tester",
        ClusterIndex = Cluster,
        HomeLocationId = 1,
        WorkplaceLocationId = 1,
        CurrentLocationId = 1,
        Properties = new Dictionary<string, float>(),
        Trust = new Dictionary<int, float>(),
    };

    private static TaleManager MakeManager(StoryletLibrary library, SpatialModel model, params int[] ids)
    {
        var tm = new TaleManager();
        tm.Initialize(library);
        tm.SetSpatialModelForTests(Cluster, model);
        foreach (int id in ids) tm.RegisterNpc(MakeNpc(id));
        return tm;
    }

    private static string Describe(NpcSchedule s) =>
        $"{s.CurrentStorylet}@{s.CurrentLocationId} {s.CurrentStart:O}-{s.CurrentEnd:O} pos={s.CurrentWorldPosition}";

    [Fact]
    public void SameSeedSamePositions()
    {
        var library = MakeLibrary();
        var model = MakeModel();
        var plain = MakeManager(library, model, 10, 11);
        var noisy = MakeManager(library, model, 10, 11, 99);

        var now = T0;
        for (int step = 0; step < 20; step++)
        {
            plain.AdvanceNpc(10, now);
            plain.AdvanceNpc(11, now);

            // An unrelated NPC advanced in between, a varying number of times.
            noisy.AdvanceNpc(10, now);
            for (int k = 0; k <= step % 3; k++) noisy.AdvanceNpc(99, now);
            noisy.AdvanceNpc(11, now);

            Assert.Equal(Describe(plain.GetSchedule(10)), Describe(noisy.GetSchedule(10)));
            Assert.Equal(Describe(plain.GetSchedule(11)), Describe(noisy.GetSchedule(11)));
            now = now.AddMinutes(45);
        }

        // The street choice must actually vary, or the test proves nothing.
        var seen = new HashSet<int>();
        var probe = MakeManager(library, model, 10);
        for (int step = 0; step < 20; step++)
        {
            probe.AdvanceNpc(10, T0.AddMinutes(45 * step));
            seen.Add(probe.GetSchedule(10).CurrentLocationId);
        }
        Assert.True(seen.Count > 2, $"expected varied streets, saw {seen.Count}");
    }

    [Fact]
    public void CurrentWorldPositionIsEntryPosition()
    {
        var library = MakeLibrary();
        var model = MakeModel();
        var tm = MakeManager(library, model, 7, 8, 9, 10);

        foreach (int id in new[] { 7, 8, 9, 10 })
        {
            Assert.NotNull(tm.AdvanceNpc(id, T0));
            var npc = tm.GetSchedule(id);
            var loc = model.GetLocation(npc.CurrentLocationId);
            Assert.Equal("street_segment", loc.Type);
            Assert.Equal(loc.EntryPositionFor(id), npc.CurrentWorldPosition);
            Assert.NotEqual(loc.Position, npc.CurrentWorldPosition);
        }
    }

    [Fact]
    public void StreetLocationsAreCachedInLocationOrder()
    {
        var model = MakeModel();
        Assert.Equal(12, model.StreetLocations.Count);
        Assert.Equal(100, model.StreetLocations[0].Id);
        Assert.Equal(111, model.StreetLocations[11].Id);
    }
}
