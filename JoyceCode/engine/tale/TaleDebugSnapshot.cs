using System;
using System.Collections.Generic;
using System.Numerics;

namespace engine.tale;

/// <summary>
/// One group as the debug panel shows it (TALE S0 W8). Membership comes from
/// NpcSchedule.GroupId, as in SocialMetrics; name, type and hangout from the
/// group table.
/// </summary>
public sealed record TaleDebugGroup(
    int GroupId,
    string Name,
    string Type,
    IReadOnlyList<int> MemberIds,
    int HangoutLocationId,
    float? HangoutDistance,
    int MembersWhoMetPlayer,
    double? MeanPlayerTrust,
    float Standing = 0f);


/// <summary>One NPC's trust in the player, for the standing list.</summary>
public sealed record TaleDebugPlayerTrust(int NpcId, string Role, int GroupId, float Trust);


/// <summary>
/// The selected NPC in full. Copies only: nothing here is shared with the
/// schedule it came from.
/// </summary>
public sealed record TaleDebugNpc(
    int NpcId,
    int ClusterIndex,
    string Role,
    string CurrentStorylet,
    DateTime CurrentStart,
    DateTime CurrentEnd,
    int CurrentLocationId,
    string CurrentLocationType,
    int HomeLocationId,
    int WorkplaceLocationId,
    bool IsInTransit,
    int TransitToLocationId,
    string NextForcedStorylet,
    int GroupId,
    string GroupName,
    string GroupType,
    float? PlayerTrust,
    bool IsMaterialized,
    bool IsTier2,
    bool HasPlayerDeviation,
    bool IsKnownToPlayer,
    IReadOnlyList<KeyValuePair<string, float>> Properties,
    IReadOnlyList<KeyValuePair<int, float>> StrongestTrust);


/// <summary>
/// Everything the ImGui TALE panel shows, copied out of TaleManager on the
/// logical thread (TALE S0 W8). ImGui renders on the render thread, so the
/// panel reads only this; the manager may change any time after it was built.
/// </summary>
public sealed record TaleDebugSnapshot(
    int ClusterIndex,
    bool IsPlayersCluster,
    IReadOnlyList<int> PopulatedClusters,
    DateTime GameNow,
    Vector3 PlayerPosition,
    int NpcCount,
    int Materialized,
    int Tier2,
    int KnownToPlayer,
    int InTransit,
    SocialMetrics Metrics,
    IReadOnlyList<float> ChurnPerDay,
    IReadOnlyList<TaleDebugGroup> Groups,
    IReadOnlyList<int> MatrixGroupIds,
    IReadOnlyList<IReadOnlyList<double?>> InterGroupMatrix,
    IReadOnlyList<TaleDebugPlayerTrust> PlayerStanding,
    TaleDebugNpc Selected,
    StandingReading? StandingLevel = null)
{
    /// <summary>The inter-group matrix shows at most this many groups, the largest.</summary>
    public const int MatrixSize = 12;

    /// <summary>The selected NPC lists at most this many trust entries.</summary>
    public const int StrongestTrustCount = 8;
}


/// <summary>
/// The hand-over between TaleModule (logical thread, writes) and the ImGui
/// panel (render thread, reads). Registered by TaleModule.
/// </summary>
public sealed class TaleDebugState
{
    /// <summary>The newest snapshot, or null before the first one.</summary>
    public volatile TaleDebugSnapshot Latest;

    /// <summary>An NPC id typed into the panel; -1 follows the inspector's selection.</summary>
    public volatile int RequestedNpcId = -1;

    /// <summary>The cluster the panel asks for; -1 is the player's cluster.</summary>
    public volatile int RequestedClusterIndex = -1;

    private readonly object _lo = new();
    private DefaultEcs.Entity _selectedEntity;

    /// <summary>
    /// The entity selected in the inspector, set by the panel. Only the
    /// logical thread may look into it (TaleNpcId).
    /// </summary>
    public DefaultEcs.Entity SelectedEntity
    {
        get { lock (_lo) return _selectedEntity; }
        set { lock (_lo) _selectedEntity = value; }
    }
}
