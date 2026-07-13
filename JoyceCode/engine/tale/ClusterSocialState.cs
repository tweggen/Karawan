using System;
using System.Collections.Generic;

namespace engine.tale;

/// <summary>
/// A faction/group as it exists at runtime. Unlike the bake-time
/// DetectedGroup (which lives only inside DesSimulation), RuntimeGroup keeps
/// the roster, type and display name available to gameplay: dialogue,
/// storylet selection and co-location bias all read from here.
/// </summary>
public class RuntimeGroup
{
    public int GroupId;

    /// <summary>"patrol_unit" / "criminal" / "trade" / "social".</summary>
    public string Type = "social";

    /// <summary>Display name, generated once (deterministic per cluster+group).</summary>
    public string Name = "";

    /// <summary>Real NpcIds of the members, ascending.</summary>
    public List<int> MemberIds = new();

    /// <summary>
    /// Preferred social venue for members (Phase E5 co-location bias);
    /// -1 = none assigned.
    /// </summary>
    public int HangoutLocationId = -1;

    /// <summary>
    /// True when the group was created by a storylet verb (form/join) rather
    /// than detection. Authored groups are never dissolved by re-detection.
    /// </summary>
    public bool Authored;
}


/// <summary>
/// Per-NPC social snapshot taken when a cluster is depopulated, so the
/// evolved social fabric survives the player leaving and returning.
/// Keyed by NpcId, which is deterministic per (cluster, npcIndex) — see
/// NpcSchedule.MakeNpcId — so re-generated schedules match their snapshots.
/// </summary>
public class NpcSocialSnapshot
{
    public int GroupId = -1;
    public Dictionary<int, float> Trust = new();
    public Dictionary<string, float> SocialProps = new();
}


/// <summary>
/// Per-cluster social state owned by TaleManager. Holds what NpcSchedule
/// cannot: the group table (roster/type/name) and the depopulation
/// snapshots. Trust itself stays single-sourced in NpcSchedule.Trust.
///
/// Lifetime: created at first PopulateCluster, NOT removed on
/// DepopulateCluster — the snapshots inside are what carry social evolution
/// across populate cycles within a session.
/// </summary>
public class ClusterSocialState
{
    /// <summary>Property keys captured into snapshots. Matches
    /// ScenarioApplicator.OverwrittenProperties.</summary>
    public static readonly string[] SnapshotProperties =
        { "morality", "wealth", "fear", "anger", "reputation" };

    public int ClusterIndex;

    public Dictionary<int, RuntimeGroup> Groups = new();

    /// <summary>Next id for runtime-created groups; initialized above the
    /// highest scenario group rank so baked and evolved ids never collide.</summary>
    public int NextGroupId = 1;

    /// <summary>Snapshots from the last depopulation, keyed by NpcId.</summary>
    public Dictionary<int, NpcSocialSnapshot> Snapshots = new();


    /// <summary>
    /// Capture one NPC's social state into the snapshot table.
    /// </summary>
    public void SnapshotNpc(NpcSchedule npc)
    {
        var snap = new NpcSocialSnapshot { GroupId = npc.GroupId };
        if (npc.Trust != null)
        {
            foreach (var (id, t) in npc.Trust) snap.Trust[id] = t;
        }
        if (npc.Properties != null)
        {
            foreach (var key in SnapshotProperties)
            {
                if (npc.Properties.TryGetValue(key, out float v))
                    snap.SocialProps[key] = v;
            }
        }
        Snapshots[npc.NpcId] = snap;
    }


    /// <summary>
    /// Restore a previously captured snapshot onto a freshly regenerated
    /// schedule. Returns false when no snapshot exists for the NPC.
    /// The snapshot wins over whatever the baked scenario applied — evolved
    /// state trumps the static bake.
    /// </summary>
    public bool RestoreNpc(NpcSchedule npc)
    {
        if (!Snapshots.TryGetValue(npc.NpcId, out var snap))
            return false;

        npc.GroupId = snap.GroupId;
        npc.Trust ??= new Dictionary<int, float>();
        npc.Trust.Clear();
        foreach (var (id, t) in snap.Trust) npc.Trust[id] = t;

        npc.Properties ??= new Dictionary<string, float>();
        foreach (var (key, v) in snap.SocialProps) npc.Properties[key] = v;
        return true;
    }
}
