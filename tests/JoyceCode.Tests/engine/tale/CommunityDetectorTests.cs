using System.Collections.Generic;
using System.Linq;
using engine.tale;
using Xunit;

namespace JoyceCode.Tests.engine.tale;

/// <summary>
/// CommunityDetector tests. The detector replaces GroupDetector's maximal
/// cliques in the scenario bake, so its determinism is part of the
/// seedability contract: same trust edges in, identical communities out.
/// Uses the DetectFromSchedules entry point (per-NPC Trust dicts) so no
/// RelationshipTracker setup is needed.
/// </summary>
public class CommunityDetectorTests
{
    private static NpcSchedule MakeNpc(int id, string role = "worker",
        float wealth = 0.5f, float morality = 0.6f)
    {
        return new NpcSchedule
        {
            NpcId = id,
            Seed = id,
            Role = role,
            Properties = new Dictionary<string, float>
            {
                { "wealth", wealth },
                { "morality", morality },
                { "reputation", 0.5f },
                { "anger", 0f },
                { "fear", 0f }
            },
            Trust = new Dictionary<int, float>()
        };
    }

    private static void Bond(Dictionary<int, NpcSchedule> npcs, int a, int b, float trust)
    {
        npcs[a].Trust[b] = trust;
        npcs[b].Trust[a] = trust;
    }

    /// <summary>Two well-separated triangles plus one isolated NPC.</summary>
    private static Dictionary<int, NpcSchedule> MakeTwoTriangles()
    {
        var npcs = Enumerable.Range(1, 7).Select(i => MakeNpc(i)).ToDictionary(n => n.NpcId);
        Bond(npcs, 1, 2, 0.9f);
        Bond(npcs, 2, 3, 0.9f);
        Bond(npcs, 1, 3, 0.9f);
        Bond(npcs, 4, 5, 0.8f);
        Bond(npcs, 5, 6, 0.8f);
        Bond(npcs, 4, 6, 0.8f);
        // Npc 7 has only a weak link below the threshold.
        Bond(npcs, 7, 1, 0.2f);
        return npcs;
    }

    [Fact]
    public void Detect_TwoTriangles_YieldsTwoDisjointCommunities()
    {
        var detector = new CommunityDetector();
        var npcs = MakeTwoTriangles();
        var result = detector.DetectFromSchedules(npcs);

        Assert.Equal(2, result.TotalGroups);
        Assert.Equal(new List<int> { 1, 2, 3 }, result.Groups[0].MemberIds);
        Assert.Equal(new List<int> { 4, 5, 6 }, result.Groups[1].MemberIds);
        Assert.Equal(6, result.NpcsInGroups);
        Assert.Equal(-1, npcs[7].GroupId);
        Assert.Equal(npcs[1].GroupId, npcs[2].GroupId);
        Assert.NotEqual(npcs[1].GroupId, npcs[4].GroupId);
    }

    [Fact]
    public void Detect_IsDeterministic()
    {
        var r1 = new CommunityDetector().DetectFromSchedules(MakeTwoTriangles());
        var r2 = new CommunityDetector().DetectFromSchedules(MakeTwoTriangles());

        Assert.Equal(r1.TotalGroups, r2.TotalGroups);
        for (int i = 0; i < r1.Groups.Count; i++)
        {
            Assert.Equal(r1.Groups[i].GroupId, r2.Groups[i].GroupId);
            Assert.Equal(r1.Groups[i].Type, r2.Groups[i].Type);
            Assert.Equal(r1.Groups[i].MemberIds, r2.Groups[i].MemberIds);
        }
    }

    [Fact]
    public void Detect_CommunitiesAreDisjoint()
    {
        // Densely connected blob bridging two triangles: whatever the
        // partition, no NPC may appear in two communities.
        var npcs = MakeTwoTriangles();
        Bond(npcs, 3, 4, 0.9f);
        var result = new CommunityDetector().DetectFromSchedules(npcs);

        var seen = new HashSet<int>();
        foreach (var g in result.Groups)
            foreach (int id in g.MemberIds)
                Assert.True(seen.Add(id), $"npc {id} is in more than one community");
    }

    [Fact]
    public void Detect_FiltersBelowMinCommunitySize()
    {
        var npcs = new[] { MakeNpc(1), MakeNpc(2) }.ToDictionary(n => n.NpcId);
        Bond(npcs, 1, 2, 0.9f);
        var result = new CommunityDetector().DetectFromSchedules(npcs);

        Assert.Equal(0, result.TotalGroups);
        Assert.Equal(-1, npcs[1].GroupId);
    }

    [Fact]
    public void Detect_IgnoresEdgesBelowTrustThreshold()
    {
        var npcs = new[] { MakeNpc(1), MakeNpc(2), MakeNpc(3) }.ToDictionary(n => n.NpcId);
        Bond(npcs, 1, 2, 0.5f);
        Bond(npcs, 2, 3, 0.5f);
        Bond(npcs, 1, 3, 0.5f);
        var detector = new CommunityDetector { TrustThreshold = 0.6f };

        Assert.Equal(0, detector.DetectFromSchedules(npcs).TotalGroups);
    }

    [Fact]
    public void Detect_ClearsStaleGroupIds()
    {
        var npcs = MakeTwoTriangles();
        var detector = new CommunityDetector();
        detector.DetectFromSchedules(npcs);
        Assert.NotEqual(-1, npcs[1].GroupId);

        // Trust collapses; the next detection must clear membership.
        foreach (var npc in npcs.Values) npc.Trust.Clear();
        detector.DetectFromSchedules(npcs);
        Assert.All(npcs.Values, npc => Assert.Equal(-1, npc.GroupId));
    }

    [Fact]
    public void Detect_EmptyGraph_ReturnsEmptyResult()
    {
        var npcs = new[] { MakeNpc(1) }.ToDictionary(n => n.NpcId);
        var result = new CommunityDetector().DetectFromSchedules(npcs);
        Assert.Equal(0, result.TotalGroups);
        Assert.Empty(result.Groups);
    }

    [Fact]
    public void Detect_MutualTopK_SplitsWeaklyBridgedBlobs()
    {
        // Two complete 4-cliques joined by a single bridge edge. With
        // MaxEdgesPerNode = 3 the bridge is outside both endpoints' top-3
        // (their intra-blob ties fill the budget), so the blobs separate.
        var npcs = Enumerable.Range(1, 8).Select(i => MakeNpc(i)).ToDictionary(n => n.NpcId);
        for (int a = 1; a <= 4; a++)
            for (int b = a + 1; b <= 4; b++)
                Bond(npcs, a, b, 0.9f);
        for (int a = 5; a <= 8; a++)
            for (int b = a + 1; b <= 8; b++)
                Bond(npcs, a, b, 0.9f);
        Bond(npcs, 4, 5, 0.9f);

        var detector = new CommunityDetector { MaxEdgesPerNode = 3 };
        var result = detector.DetectFromSchedules(npcs);

        Assert.Equal(2, result.TotalGroups);
        Assert.Equal(new List<int> { 1, 2, 3, 4 }, result.Groups[0].MemberIds);
        Assert.Equal(new List<int> { 5, 6, 7, 8 }, result.Groups[1].MemberIds);

        // Sanity: without sparsification the same graph is one community.
        var unsparsified = new CommunityDetector { MaxEdgesPerNode = 0 };
        foreach (var npc in npcs.Values) npc.GroupId = -1;
        Assert.Equal(1, unsparsified.DetectFromSchedules(npcs).TotalGroups);
    }

    [Fact]
    public void Detect_MutualTopK_DropsOneSidedNominations()
    {
        // Equal-weight triangle with K=1: ties break to the lowest id, so
        // only the (1,2) edge is nominated from both ends. The surviving
        // pair is below MinCommunitySize.
        var npcs = new[] { MakeNpc(1), MakeNpc(2), MakeNpc(3) }.ToDictionary(n => n.NpcId);
        Bond(npcs, 1, 2, 0.9f);
        Bond(npcs, 2, 3, 0.9f);
        Bond(npcs, 1, 3, 0.9f);

        var detector = new CommunityDetector { MaxEdgesPerNode = 1 };
        Assert.Equal(0, detector.DetectFromSchedules(npcs).TotalGroups);
    }

    [Fact]
    public void Detect_IgnoresPlayerTrustEntry()
    {
        // Trust[-1] is the NPC's trust toward the player; it must never
        // create a phantom graph node.
        var npcs = MakeTwoTriangles();
        npcs[1].Trust[-1] = 1.0f;
        var result = new CommunityDetector().DetectFromSchedules(npcs);
        Assert.Equal(2, result.TotalGroups);
        Assert.All(result.Groups, g => Assert.DoesNotContain(-1, g.MemberIds));
    }
}
