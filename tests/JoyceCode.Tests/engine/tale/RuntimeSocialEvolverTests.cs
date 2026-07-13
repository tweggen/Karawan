using System;
using System.Collections.Generic;
using System.Linq;
using engine.tale;
using Xunit;

namespace JoyceCode.Tests.engine.tale;

/// <summary>
/// RuntimeSocialEvolver tests (TALE-SOCIAL Phase E3). The evolver is the
/// live game's replacement for the bake's EncounterResolver+GroupDetector
/// pipeline; determinism matters because encounter RNG must not depend on
/// process state. No InteractionTypeRegistry is registered in these tests,
/// so every encounter falls back to "greet" (+0.04 trust) — which is exactly
/// what the deterministic assertions rely on.
/// </summary>
public class RuntimeSocialEvolverTests
{
    private static readonly DateTime T0 = new(2000, 1, 2, 18, 0, 0);

    private static NpcSchedule MakeNpc(int id, int locationId)
    {
        return new NpcSchedule
        {
            NpcId = id,
            Seed = id,
            Role = "worker",
            ClusterIndex = 1,
            CurrentLocationId = locationId,
            Properties = new Dictionary<string, float>
            {
                { "wealth", 0.5f }, { "morality", 0.6f }, { "reputation", 0.5f },
                { "anger", 0f }, { "fear", 0.1f }
            },
            Trust = new Dictionary<int, float>()
        };
    }

    private static SpatialModel MakeVenueModel(params int[] venueIds)
    {
        var model = new SpatialModel();
        foreach (int id in venueIds)
            model.Locations.Add(new Location { Id = id, Type = "social_venue" });
        model.BuildIndex();
        return model;
    }

    /// <summary>Ticks twice so the first call (which only arms the window)
    /// is followed by one with a real co-presence window.</summary>
    private static RuntimeSocialEvolver.TickStats TickTwice(
        RuntimeSocialEvolver evolver, List<NpcSchedule> npcs, SpatialModel spatial)
    {
        evolver.TickEncounters(1, npcs, spatial, T0);
        return evolver.TickEncounters(1, npcs, spatial, T0.AddMinutes(30));
    }

    [Fact]
    public void Tick_CoLocatedNpcsGainTrustDeterministically()
    {
        // 6 NPCs in one venue, 30-minute window at P=0.07/15min → encounters
        // are all but certain across 15 pairs; assert determinism by running
        // the identical setup twice.
        List<NpcSchedule> Make() => Enumerable.Range(1, 6).Select(i => MakeNpc(i, 500)).ToList();
        var spatial = MakeVenueModel(500);

        var npcsA = Make();
        var statsA = TickTwice(new RuntimeSocialEvolver(), npcsA, spatial);
        var npcsB = Make();
        var statsB = TickTwice(new RuntimeSocialEvolver(), npcsB, spatial);

        Assert.True(statsA.Encounters > 0, "expected at least one encounter");
        Assert.Equal(statsA.Encounters, statsB.Encounters);
        for (int i = 0; i < npcsA.Count; i++)
        {
            Assert.Equal(npcsA[i].Trust.Count, npcsB[i].Trust.Count);
            foreach (var (id, t) in npcsA[i].Trust)
                Assert.Equal(t, npcsB[i].Trust[id]);
        }

        // Trust must be symmetric-in-existence: greet applies both directions.
        foreach (var npc in npcsA)
            foreach (var (otherId, t) in npc.Trust)
            {
                Assert.Equal(0.04f, t, 5);
                Assert.Equal(0.04f, npcsA.First(n => n.NpcId == otherId).Trust[npc.NpcId], 5);
            }
    }

    [Fact]
    public void Tick_ExcludesNpcsInTransit()
    {
        var npcs = new List<NpcSchedule> { MakeNpc(1, 500), MakeNpc(2, 500) };
        npcs[1].IsInTransit = true;
        npcs[1].TransitEnd = T0.AddHours(2); // still traveling at tick time

        var stats = TickTwice(new RuntimeSocialEvolver(), npcs, MakeVenueModel(500));

        Assert.Equal(1, stats.NpcsPresent);
        Assert.Equal(0, stats.PairsSampled);
        Assert.Empty(npcs[0].Trust);
    }

    [Fact]
    public void Tick_SkipsHomeLocations()
    {
        var npcs = new List<NpcSchedule> { MakeNpc(1, 500), MakeNpc(2, 500) };
        var model = new SpatialModel();
        model.Locations.Add(new Location { Id = 500, Type = "home" });

        var stats = TickTwice(new RuntimeSocialEvolver(), npcs, model);

        Assert.Equal(0, stats.LocationsWithCompany);
        Assert.Equal(0, stats.Encounters);
    }

    [Fact]
    public void Tick_DailyDedupPreventsRepeatEncounters()
    {
        var npcs = new List<NpcSchedule> { MakeNpc(1, 500), MakeNpc(2, 500) };
        var spatial = MakeVenueModel(500);
        var evolver = new RuntimeSocialEvolver { P_Venue = 1.0f }; // guarantee encounters

        evolver.TickEncounters(1, npcs, spatial, T0);
        var s1 = evolver.TickEncounters(1, npcs, spatial, T0.AddMinutes(30));
        var s2 = evolver.TickEncounters(1, npcs, spatial, T0.AddMinutes(60));

        Assert.Equal(1, s1.Encounters);
        Assert.Equal(0, s2.Encounters); // same pair, same day → deduped
        Assert.Equal(0.04f, npcs[0].Trust[2], 5); // only one greet applied

        // Next day the dedup resets and the pair can meet again.
        var s3 = evolver.TickEncounters(1, npcs, spatial, T0.AddDays(1));
        Assert.Equal(1, s3.Encounters);
        Assert.Equal(0.08f, npcs[0].Trust[2], 5);
    }

    [Fact]
    public void Tick_CapsSampledPairsPerLocation()
    {
        // 20 NPCs in one venue = 190 possible pairs; the cap keeps per-tick
        // work bounded.
        var npcs = Enumerable.Range(1, 20).Select(i => MakeNpc(i, 500)).ToList();
        var evolver = new RuntimeSocialEvolver { MaxPairsPerLocation = 12 };

        var stats = TickTwice(evolver, npcs, MakeVenueModel(500));

        Assert.True(stats.PairsSampled <= 12,
            $"sampled {stats.PairsSampled} pairs, cap is 12");
    }

    [Fact]
    public void Redetect_KeepsGroupIdentityOnMajorityOverlap()
    {
        // Existing group 3 has members 1-4; the evolved trust graph yields a
        // community of 1-4 plus newcomer 5 → identity (id + name) must carry.
        var npcs = Enumerable.Range(1, 5).Select(i => MakeNpc(i, 500)).ToDictionary(n => n.NpcId);
        foreach (var a in npcs.Values)
            foreach (var b in npcs.Values)
                if (a.NpcId < b.NpcId) { a.Trust[b.NpcId] = 0.9f; b.Trust[a.NpcId] = 0.9f; }

        var social = new ClusterSocialState { ClusterIndex = 1, NextGroupId = 10 };
        social.Groups[3] = new RuntimeGroup
        {
            GroupId = 3, Type = "social", Name = "The Old Circle",
            MemberIds = new List<int> { 1, 2, 3, 4 }
        };

        var stats = new RuntimeSocialEvolver().Redetect(1, social, npcs, (c, g, t) => $"gen-{g}");

        Assert.Equal(1, stats.Communities);
        Assert.Equal(1, stats.KeptIdentity);
        Assert.Equal(0, stats.Created);
        var group = Assert.Single(social.Groups).Value;
        Assert.Equal(3, group.GroupId);
        Assert.Equal("The Old Circle", group.Name);
        Assert.Equal(new List<int> { 1, 2, 3, 4, 5 }, group.MemberIds);
        Assert.All(npcs.Values, n => Assert.Equal(3, n.GroupId));
    }

    [Fact]
    public void Redetect_CreatesNewGroupAndDissolvesDeadOne()
    {
        // Existing group 0 (members 1-3) has lost all trust; a fresh trio
        // 4-6 has bonded instead.
        var npcs = Enumerable.Range(1, 6).Select(i => MakeNpc(i, 500)).ToDictionary(n => n.NpcId);
        foreach (var (a, b) in new[] { (4, 5), (5, 6), (4, 6) })
        {
            npcs[a].Trust[b] = 0.9f;
            npcs[b].Trust[a] = 0.9f;
        }

        var social = new ClusterSocialState { ClusterIndex = 1, NextGroupId = 1 };
        social.Groups[0] = new RuntimeGroup
        {
            GroupId = 0, Type = "social", Name = "The Faded Regulars",
            MemberIds = new List<int> { 1, 2, 3 }
        };

        var stats = new RuntimeSocialEvolver().Redetect(1, social, npcs, (c, g, t) => $"gen-{g}");

        Assert.Equal(1, stats.Created);
        Assert.Equal(1, stats.Dissolved);
        var group = Assert.Single(social.Groups).Value;
        Assert.Equal(1, group.GroupId); // from NextGroupId
        Assert.Equal("gen-1", group.Name);
        Assert.Equal(new List<int> { 4, 5, 6 }, group.MemberIds);
        Assert.Equal(-1, npcs[1].GroupId); // dissolved members ungrouped
        Assert.Equal(1, npcs[4].GroupId);
    }

    [Fact]
    public void Redetect_AuthoredGroupSurvivesWithoutTrustSupport()
    {
        // Authored (storylet-formed) gang has no high-trust edges yet; it must
        // survive re-detection and keep its members.
        var npcs = Enumerable.Range(1, 3).Select(i => MakeNpc(i, 500)).ToDictionary(n => n.NpcId);
        foreach (var n in npcs.Values) n.GroupId = 7;

        var social = new ClusterSocialState { ClusterIndex = 1, NextGroupId = 8 };
        social.Groups[7] = new RuntimeGroup
        {
            GroupId = 7, Type = "criminal", Name = "The Stray Dogs",
            MemberIds = new List<int> { 1, 2, 3 }, Authored = true
        };

        var stats = new RuntimeSocialEvolver().Redetect(1, social, npcs, (c, g, t) => $"gen-{g}");

        Assert.Equal(0, stats.Dissolved);
        var group = Assert.Single(social.Groups).Value;
        Assert.Equal(7, group.GroupId);
        Assert.True(group.Authored);
        Assert.All(npcs.Values, n => Assert.Equal(7, n.GroupId));
    }
}
