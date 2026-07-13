using System.Collections.Generic;
using engine.tale;
using Xunit;

namespace JoyceCode.Tests.engine.tale;

/// <summary>
/// ClusterSocialState snapshot/restore tests (TALE-SOCIAL Phase E2). The
/// snapshot table is what carries evolved trust/groups across cluster
/// depopulate/repopulate cycles, keyed by the deterministic NpcId.
/// </summary>
public class ClusterSocialStateTests
{
    private static NpcSchedule MakeNpc(int id, int groupId = -1)
    {
        return new NpcSchedule
        {
            NpcId = id,
            Seed = id,
            Role = "worker",
            GroupId = groupId,
            Properties = new Dictionary<string, float>
            {
                { "morality", 0.6f },
                { "wealth", 0.4f },
                { "fear", 0.1f },
                { "anger", 0.2f },
                { "reputation", 0.5f },
                { "hunger", 0.7f }
            },
            Trust = new Dictionary<int, float>()
        };
    }

    [Fact]
    public void SnapshotAndRestore_RoundTripsSocialState()
    {
        var state = new ClusterSocialState { ClusterIndex = 3 };
        var npc = MakeNpc(42, groupId: 7);
        npc.Trust[43] = 0.8f;
        npc.Trust[-1] = 0.3f; // player trust must survive too
        state.SnapshotNpc(npc);

        // Simulate depopulate/repopulate: a fresh schedule with generator
        // defaults and scenario-applied values.
        var fresh = MakeNpc(42, groupId: 2);
        fresh.Properties["morality"] = 0.1f;
        fresh.Trust[99] = 0.9f;

        Assert.True(state.RestoreNpc(fresh));
        Assert.Equal(7, fresh.GroupId);
        Assert.Equal(0.8f, fresh.Trust[43]);
        Assert.Equal(0.3f, fresh.Trust[-1]);
        Assert.False(fresh.Trust.ContainsKey(99)); // stale bake trust replaced
        Assert.Equal(0.6f, fresh.Properties["morality"]);
    }

    [Fact]
    public void Restore_LeavesNonSocialPropertiesAlone()
    {
        var state = new ClusterSocialState();
        var npc = MakeNpc(1);
        state.SnapshotNpc(npc);

        var fresh = MakeNpc(1);
        fresh.Properties["hunger"] = 0.05f; // warmup state, not social
        state.RestoreNpc(fresh);

        Assert.Equal(0.05f, fresh.Properties["hunger"]);
    }

    [Fact]
    public void Restore_WithoutSnapshot_ReturnsFalse()
    {
        var state = new ClusterSocialState();
        var npc = MakeNpc(1, groupId: 5);
        Assert.False(state.RestoreNpc(npc));
        Assert.Equal(5, npc.GroupId); // untouched
    }

    [Fact]
    public void Snapshot_OverwritesPreviousSnapshotForSameNpc()
    {
        var state = new ClusterSocialState();
        var npc = MakeNpc(1, groupId: 2);
        state.SnapshotNpc(npc);
        npc.GroupId = 9;
        state.SnapshotNpc(npc);

        var fresh = MakeNpc(1);
        state.RestoreNpc(fresh);
        Assert.Equal(9, fresh.GroupId);
    }
}
