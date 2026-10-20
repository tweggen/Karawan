using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using engine.joyce;
using engine.tale;
using engine.world;
using Xunit;

namespace JoyceCode.Tests.engine.tale;

/// <summary>
/// TALE S0 W3 (KAR-447): despawn follows the entity, and the player's NPCs are
/// kept explicitly. An entity dies with the fragment that created it, not with
/// the fragment its schedule position lies in; dematerialising an NPC never
/// removes its schedule.
/// </summary>
public class DespawnBookkeepingTests
{
    private const int Cluster = 2;

    // Two neighbouring fragments: A creates the entity, the NPC walks into B.
    private static readonly Vector3 PosA = new(10f, 0f, 10f);
    private static readonly Vector3 PosB = PosA + new Vector3(MetaGen.FragmentSize, 0f, 0f);
    private static readonly Index3 FragA = Fragment.PosToIndex3(PosA);
    private static readonly Index3 FragB = Fragment.PosToIndex3(PosB);

    private static NpcSchedule MakeNpc(int npcIndex, Vector3 at) => new()
    {
        NpcId = (Cluster << 12) | npcIndex,
        NpcIndex = npcIndex,
        ClusterIndex = Cluster,
        Role = "tester",
        HomePosition = at,
        CurrentWorldPosition = at,
        Properties = new Dictionary<string, float>(),
        Trust = new Dictionary<int, float>(),
    };

    private static bool InFragment(TaleManager tm, int npcId, Index3 frag)
        => tm.GetNpcsInFragment(frag).Any(n => n.NpcId == npcId);


    [Fact]
    public void Fragments_AreDistinct()
    {
        Assert.False(FragA.I == FragB.I && FragA.K == FragB.K);
    }


    [Fact]
    public void Dematerialize_KeepsScheduleTrustAndFacts()
    {
        var tm = new TaleManager();
        var npc = MakeNpc(1, PosA);
        tm.RegisterNpc(npc);
        npc.Trust[-1] = 0.6f;
        npc.Properties["player_fact.helped"] = 1f;
        tm.MarkPlayerContact(npc);
        npc.LastConversationTime = DateTime.UtcNow.AddHours(-1);
        tm.SetMaterialized(npc.NpcId);

        tm.OnNpcDematerialized(npc.NpcId, PosB);

        var kept = tm.GetSchedule(npc.NpcId);
        Assert.Same(npc, kept);
        Assert.Equal(0.6f, kept.Trust[-1]);
        Assert.Equal(1f, kept.Properties["player_fact.helped"]);
        Assert.True(kept.HasPlayerDeviation);
        Assert.False(tm.IsMaterialized(npc.NpcId));
        Assert.True(InFragment(tm, npc.NpcId, FragB));
        Assert.False(InFragment(tm, npc.NpcId, FragA));
        Assert.Equal(0, tm.UnflaggedPlayerContacts);
    }


    [Fact]
    public void Dematerialize_WithoutPosition_KeepsLastPosition()
    {
        var tm = new TaleManager();
        var npc = MakeNpc(2, PosA);
        tm.RegisterNpc(npc);
        tm.SetMaterialized(npc.NpcId);

        tm.OnNpcDematerialized(npc.NpcId, null);

        Assert.False(tm.IsMaterialized(npc.NpcId));
        Assert.Equal(PosA, tm.GetSchedule(npc.NpcId).CurrentWorldPosition);
    }


    [Fact]
    public void MarkPlayerContact_FlagsAndAddsToSkipMask()
    {
        var tm = new TaleManager();
        var npc = MakeNpc(7, PosA);
        tm.RegisterNpc(npc);
        Assert.Null(tm.GetDeviationSkipMask(Cluster));

        tm.MarkPlayerContact(npc);

        Assert.True(npc.HasPlayerDeviation);
        Assert.Contains(7, tm.GetDeviationSkipMask(Cluster));
    }


    [Fact]
    public void IsKnownToPlayer_ByFlagTrustOrFact()
    {
        var npc = MakeNpc(3, PosA);
        Assert.False(npc.IsKnownToPlayer);
        npc.Trust[4] = 0.9f; // trust toward another NPC is not the player
        npc.Properties["wealth"] = 0.5f;
        Assert.False(npc.IsKnownToPlayer);

        npc.Trust[-1] = 0.2f;
        Assert.True(npc.IsKnownToPlayer);

        var other = MakeNpc(4, PosA);
        other.Properties["player_fact.owes"] = 1f;
        Assert.True(other.IsKnownToPlayer);

        var flagged = MakeNpc(5, PosA);
        flagged.HasPlayerDeviation = true;
        flagged.Trust = null;
        flagged.Properties = null;
        Assert.True(flagged.IsKnownToPlayer);
    }


    [Fact]
    public void KnownToPlayerWithoutFlag_WarnsAndFlags()
    {
        var tm = new TaleManager();
        var npc = MakeNpc(3, PosA);
        tm.RegisterNpc(npc);
        npc.Trust[-1] = 0.3f; // written without MarkPlayerContact

        tm.OnNpcDematerialized(npc.NpcId, null);

        Assert.Equal(1, tm.UnflaggedPlayerContacts);
        Assert.True(npc.HasPlayerDeviation);
        Assert.Contains(3, tm.GetDeviationSkipMask(Cluster));
    }


    [Fact]
    public void KnownToPlayerWithoutFlag_IsSavedAnyway()
    {
        var tm = new TaleManager();
        var npc = MakeNpc(6, PosA);
        tm.RegisterNpc(npc);
        tm.RegisterNpc(MakeNpc(8, PosA)); // stranger: not saved
        npc.Properties["player_fact.met"] = 1f;

        var saved = tm.GetAllDeviatedNpcs();

        Assert.Single(saved);
        Assert.Equal(npc.NpcId, saved[0].NpcId);
        Assert.Equal(1, tm.UnflaggedPlayerContacts);
    }


    /*
     * The owner map, with strings standing in for entities and a set of the
     * live ones as the liveness predicate.
     */
    private static TaleEntityOwnership<string> MakeOwnership(HashSet<string> alive)
        => new(e => alive.Contains(e));


    [Fact]
    public void PurgeOwner_DematerialisesNpcThatWalkedAway()
    {
        var tm = new TaleManager();
        var npc = MakeNpc(1, PosA);
        tm.RegisterNpc(npc);
        tm.SetMaterialized(npc.NpcId);
        var alive = new HashSet<string> { "e1" };
        var ownership = MakeOwnership(alive);
        ownership.Record(npc.NpcId, FragA, "e1");

        // The NPC walks into B; its entity is still owned by A.
        npc.CurrentWorldPosition = PosB;

        // Purging B (where the schedule points) must not touch it: the entity lives on in A.
        Assert.Empty(ownership.RemoveOwnedBy(FragB));
        Assert.True(tm.IsMaterialized(npc.NpcId));

        // Purging A takes the entity along: the NPC is dematerialised.
        var purged = ownership.RemoveOwnedBy(FragA);
        Assert.Equal(new[] { (npc.NpcId, "e1") }, purged);
        foreach (var (id, _) in purged) tm.OnNpcDematerialized(id, PosB);
        Assert.False(tm.IsMaterialized(npc.NpcId));
        Assert.Equal(0, ownership.Count);
    }


    [Fact]
    public void DeadEntityInLoadedFragment_IsReported()
    {
        var alive = new HashSet<string> { "e1", "e2" };
        var ownership = MakeOwnership(alive);
        ownership.Record(1, FragA, "e1");
        ownership.Record(2, FragA, "e2");
        ownership.Record(3, FragB, "e3"); // dead, but owned elsewhere

        alive.Remove("e2"); // the wiper took it

        Assert.Equal(new[] { 2 }, ownership.RemoveDeadOwnedBy(FragA));
        Assert.Empty(ownership.RemoveDeadOwnedBy(FragA));
        Assert.True(ownership.TryGet(1, out _));
    }


    [Fact]
    public void ResidentCount_ExcludesInCreationAndDead()
    {
        var alive = new HashSet<string> { "e1", "e3" };
        var ownership = MakeOwnership(alive);
        ownership.Record(1, FragA, "e1");   // alive, owned here
        ownership.Record(2, FragA, "e2");   // dead
        ownership.Record(3, FragB, "e3");   // alive, owned by a neighbour, stands here
        // NPC 4 is in creation: materialised but not yet recorded.

        Assert.Equal(2, ownership.CountAlive(new[] { 1, 2, 3, 4 }));
    }
}
