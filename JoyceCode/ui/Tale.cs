using System.Globalization;
using System.Linq;
using engine;
using engine.tale;
using ImGuiNET;

namespace joyce.ui;

/**
 * The TALE pane (TALE S0 W8): one cluster's society and the selected NPC.
 *
 * ImGui renders on the render thread, so this reads only the snapshot that
 * TaleModule copies once a second while the editor is open
 * (TaleDebugState.Latest), and talks back only through TaleDebugState's
 * request fields.
 */
public class Tale : APart
{
    private const string OverlayProp = "debug.tale.overlay";

    private readonly EntityState _entityState;
    private int _typedNpcId = -1;


    private static string _f(double? v) =>
        v.HasValue ? v.Value.ToString("F2", CultureInfo.InvariantCulture) : "-";


    private static void _requestNpc(TaleDebugState state, int npcId)
    {
        state.RequestedNpcId = npcId;
    }


    private void _renderHeader(TaleDebugState state, TaleDebugSnapshot snap)
    {
        string current = snap.ClusterIndex < 0 ? "(none populated)"
            : $"cluster {snap.ClusterIndex}{(snap.IsPlayersCluster ? " (player's)" : "")}";
        if (ImGui.BeginCombo("Cluster", current))
        {
            if (ImGui.Selectable("player's cluster", state.RequestedClusterIndex < 0))
                state.RequestedClusterIndex = -1;
            foreach (int index in snap.PopulatedClusters)
            {
                if (ImGui.Selectable($"cluster {index}", state.RequestedClusterIndex == index))
                    state.RequestedClusterIndex = index;
            }
            ImGui.EndCombo();
        }

        ImGui.Text($"Game time {snap.GameNow:yyyy-MM-dd HH:mm}");
        ImGui.Text($"{snap.NpcCount} NPCs: {snap.Materialized} materialised, {snap.Tier2} tier 2, " +
                   $"{snap.InTransit} in transit, {snap.KnownToPlayer} known to the player");
        ImGui.Text("Density near the player: (W9's counter, not there yet)");

        bool overlay = (bool)Props.Get(OverlayProp, false);
        if (ImGui.Checkbox("Overlay above NPCs (debug.tale.overlay)", ref overlay))
        {
            bool value = overlay;
            _engine.QueueMainThreadAction(() => Props.Set(OverlayProp, value));
        }
    }


    private void _renderMetrics(TaleDebugSnapshot snap)
    {
        if (!ImGui.TreeNode("Metrics")) return;
        var m = snap.Metrics;
        string types = string.Join(", ", m.GroupCountByType.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key} {kv.Value}"));
        ImGui.Text($"Groups {m.GroupCount} ({types})");
        ImGui.Text($"Size min/median/max {m.GroupSizeMin}/{_f(m.GroupSizeMedian)}/{m.GroupSizeMax}, " +
                   $"in groups {m.NpcsInGroups} ({_f(m.GroupMembershipRatio)}), mismatch {m.GroupTableMismatch}");
        ImGui.Text($"Pairs {m.KnownPairs}, density {_f(m.TrustDensity)}, strong {m.StrongPairs}");
        ImGui.Text($"Trust intra {_f(m.MeanIntraGroupTrust)}, inter {_f(m.MeanInterGroupTrust)}");
        ImGui.Text($"Met the player {m.MetPlayer} ({_f(m.MetPlayerShare)}), mean trust {_f(m.MeanPlayerTrust)}");
        if (m.Bake != null)
            ImGui.Text($"Bake {m.Bake.Category}/{m.Bake.Index}: match {_f(m.Bake.MatchRatio)}, " +
                       $"coverage {_f(m.Bake.Coverage)}");
        ImGui.Text($"Churn/day {_f(m.MembershipChurnPerDay)} over {m.HistoryDays} days, " +
                   $"+{_f(m.GroupsCreatedPerDay)}/-{_f(m.GroupsDissolvedPerDay)} groups/day");

        var churn = snap.ChurnPerDay.ToArray();
        if (churn.Length > 0)
            ImGui.PlotLines("Membership changes/day", ref churn[0], churn.Length);
        ImGui.TreePop();
    }


    private void _renderGroups(TaleDebugState state, TaleDebugSnapshot snap)
    {
        if (!ImGui.TreeNode($"Groups ({snap.Groups.Count})###groups")) return;
        foreach (var g in snap.Groups)
        {
            string hangout = g.HangoutLocationId < 0 ? "no hangout"
                : $"hangout {g.HangoutLocationId}" + (g.HangoutDistance.HasValue ? $" @ {g.HangoutDistance.Value:F0} m" : "");
            string label = $"g{g.GroupId} {g.Name} [{g.Type}] x{g.MemberIds.Count}, {hangout}, " +
                           $"player {_f(g.MeanPlayerTrust)} ({g.MembersWhoMetPlayer} met)###g{g.GroupId}";
            if (ImGui.TreeNode(label))
            {
                foreach (int id in g.MemberIds)
                {
                    if (ImGui.Selectable($"NPC {id}", state.RequestedNpcId == id))
                        _requestNpc(state, id);
                }
                ImGui.TreePop();
            }
        }
        ImGui.TreePop();
    }


    private void _renderMatrix(TaleDebugSnapshot snap)
    {
        if (snap.MatrixGroupIds.Count < 2 || !ImGui.TreeNode("Inter-group trust")) return;
        int n = snap.MatrixGroupIds.Count;
        if (ImGui.BeginTable("tale_matrix", n + 1, ImGuiTableFlags.Borders | ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            foreach (int id in snap.MatrixGroupIds)
            {
                ImGui.TableNextColumn();
                ImGui.Text($"g{id}");
            }
            for (int i = 0; i < n; i++)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text($"g{snap.MatrixGroupIds[i]}");
                for (int j = 0; j < n; j++)
                {
                    ImGui.TableNextColumn();
                    ImGui.Text(_f(snap.InterGroupMatrix[i][j]));
                }
            }
            ImGui.EndTable();
        }
        ImGui.TreePop();
    }


    private void _renderStanding(TaleDebugState state, TaleDebugSnapshot snap)
    {
        if (!ImGui.TreeNode($"Player standing ({snap.PlayerStanding.Count} NPCs)###standing")) return;
        foreach (var s in snap.PlayerStanding)
        {
            string group = s.GroupId >= 0 ? $"g{s.GroupId}" : "no group";
            if (ImGui.Selectable($"NPC {s.NpcId} {s.Role}, {group}: {_f(s.Trust)}###s{s.NpcId}",
                    state.RequestedNpcId == s.NpcId))
                _requestNpc(state, s.NpcId);
        }
        ImGui.TreePop();
    }


    private void _renderSelected(TaleDebugState state, TaleDebugSnapshot snap)
    {
        ImGui.Separator();
        if (ImGui.InputInt("NPC id (-1: inspector's)", ref _typedNpcId))
            _requestNpc(state, _typedNpcId < 0 ? -1 : _typedNpcId);
        if (state.RequestedNpcId >= 0)
        {
            ImGui.SameLine();
            if (ImGui.Button("Follow inspector"))
            {
                _typedNpcId = -1;
                _requestNpc(state, -1);
            }
        }

        var npc = snap.Selected;
        if (npc == null)
        {
            ImGui.Text("No TALE NPC selected.");
            return;
        }

        ImGui.Text($"NPC {npc.NpcId} (cluster {npc.ClusterIndex}), {npc.Role}");
        ImGui.Text($"Storylet {npc.CurrentStorylet} {npc.CurrentStart:HH:mm}-{npc.CurrentEnd:HH:mm}" +
                   (npc.NextForcedStorylet != null ? $", next forced {npc.NextForcedStorylet}" : ""));
        ImGui.Text($"At {npc.CurrentLocationId} ({npc.CurrentLocationType ?? "?"})" +
                   (npc.IsInTransit ? $", in transit to {npc.TransitToLocationId}" : "") +
                   $"; home {npc.HomeLocationId}, work {npc.WorkplaceLocationId}");
        ImGui.Text(npc.GroupId >= 0 ? $"Group g{npc.GroupId} {npc.GroupName} [{npc.GroupType}]" : "No group");
        ImGui.Text($"Trust in the player {_f(npc.PlayerTrust)}");
        ImGui.Text($"Flags: {(npc.IsMaterialized ? "materialised" : "abstract")}" +
                   (npc.IsTier2 ? ", tier 2" : "") +
                   (npc.HasPlayerDeviation ? ", deviated" : "") +
                   (npc.IsKnownToPlayer ? ", known to the player" : ""));

        if (ImGui.TreeNode($"Properties ({npc.Properties.Count})###props"))
        {
            foreach (var (key, value) in npc.Properties)
                ImGui.Text($"{key} = {_f(value)}");
            ImGui.TreePop();
        }

        if (ImGui.TreeNode($"Strongest trust ({npc.StrongestTrust.Count})###trust"))
        {
            foreach (var (otherId, value) in npc.StrongestTrust)
            {
                if (ImGui.Selectable($"NPC {otherId}: {_f(value)}###t{otherId}"))
                    _requestNpc(state, otherId);
            }
            ImGui.TreePop();
        }
    }


    public override void Render(float dt)
    {
        var state = I.TryGet<TaleDebugState>();
        if (I.TryGet<TaleManager>() == null || state == null)
        {
            ImGui.Text("(TALE not active)");
            return;
        }

        state.SelectedEntity = _entityState.CurrentEntity;

        var snap = state.Latest;
        if (snap == null)
        {
            ImGui.Text("Waiting for the first snapshot...");
            return;
        }

        _renderHeader(state, snap);
        _renderMetrics(snap);
        _renderGroups(state, snap);
        _renderMatrix(snap);
        _renderStanding(state, snap);
        _renderSelected(state, snap);
    }


    public Tale(Main uiMain, EntityState entityState) : base(uiMain)
    {
        _entityState = entityState;
    }
}
