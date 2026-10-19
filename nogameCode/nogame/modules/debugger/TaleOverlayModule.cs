using System.Collections.Generic;
using System.Numerics;
using DefaultEcs;
using engine;
using engine.draw.components;
using engine.joyce;
using engine.tale;
using engine.tale.components;
using static engine.Logger;

namespace nogame.modules.debugger;

/**
 * Per-NPC TALE debug overlay (TALE S0 W7, KAR-449).
 *
 * While the property debug.tale.overlay is true, every materialised TALE NPC
 * carries a child label entity with an OSDText showing role, storylet, tier,
 * group and Trust[-1] (see engine.tale.TaleOverlayText). While it is false,
 * no label entity exists.
 *
 * Refreshed at most twice a second on the logical thread. The module keeps its
 * own npcId -> label map: the real death paths dispose NPC entities without
 * walking their children (only HierarchyApi.Delete is recursive), so a label
 * whose NPC is gone would otherwise stay behind.
 */
public class TaleOverlayModule : AModule
{
    private static readonly engine.Dc _dc = engine.Dc.TaleManager;

    public const string PropName = "debug.tale.overlay";

    public float RefreshInterval { get; set; } = 0.5f;
    public float MaxDistance { get; set; } = 40f;
    public Vector3 LabelOffset { get; set; } = new(0f, 2.2f, 0f);

    private const uint TextColor = 0xffeeee22;
    private const uint FontSize = 12;


    private class Label
    {
        public Entity ENpc;
        public Entity ELabel;
        public string Text;
    }

    /**
     * Only touched on the logical thread.
     */
    private readonly Dictionary<int, Label> _labels = new();

    private float _sinceRefresh = 0f;


    private void _deleteLabel(Label label)
    {
        if (label.ELabel.IsAlive)
        {
            label.ELabel.Dispose();
        }
    }


    private void _deleteAllLabels()
    {
        if (_labels.Count == 0) return;
        foreach (var label in _labels.Values)
        {
            _deleteLabel(label);
        }

        _labels.Clear();
    }


    private Entity _createLabel(in Entity eNpc, string text)
    {
        var eLabel = _engine.CreateEntity("nogame.debugger.taleOverlay");
        eLabel.Set(new OSDText(
            new Vector2(-100f, 0f), new Vector2(200f, 30f),
            text, FontSize, TextColor,
            0x00000000, engine.draw.HAlign.Center) { MaxDistance = MaxDistance, CameraMask = 1 });
        I.Get<HierarchyApi>().SetParent(eLabel, eNpc);
        I.Get<TransformApi>().SetTransforms(eLabel, true,
            0x00000001, Quaternion.Identity, LabelOffset);
        return eLabel;
    }


    private void _refresh()
    {
        bool isOn = (bool)engine.Props.Get(PropName, false);
        var taleManager = I.TryGet<TaleManager>();
        if (!isOn || null == taleManager)
        {
            _deleteAllLabels();
            return;
        }

        /*
         * Collect first: creating label entities while enumerating the world
         * would change the set being enumerated.
         */
        var npcs = new List<(int NpcId, Entity ENpc)>();
        foreach (var e in _engine.GetEcsWorld().GetEntities().With<TaleNpcId>().AsEnumerable())
        {
            npcs.Add((e.Get<TaleNpcId>().NpcId, e));
        }

        var seen = new HashSet<int>();
        foreach (var (npcId, eNpc) in npcs)
        {
            if (!eNpc.IsAlive || !seen.Add(npcId)) continue;

            var schedule = taleManager.GetSchedule(npcId);
            if (null == schedule) continue;

            string text;
            try
            {
                text = TaleOverlayText.Format(schedule,
                    taleManager.GetGroup(schedule.ClusterIndex, schedule.GroupId),
                    taleManager.IsTier2(npcId));
            }
            catch (System.InvalidOperationException)
            {
                /*
                 * A worker thread changed Trust while we read it. This is a
                 * debug view; try again on the next refresh.
                 */
                continue;
            }

            if (_labels.TryGetValue(npcId, out var label))
            {
                if (label.ENpc != eNpc || !label.ELabel.IsAlive)
                {
                    /*
                     * The NPC was re-materialised as a new entity, or the label
                     * went down with its parent: start over.
                     */
                    _deleteLabel(label);
                    label.ENpc = eNpc;
                    label.ELabel = _createLabel(eNpc, text);
                    label.Text = text;
                }
                else if (label.Text != text)
                {
                    label.ELabel.Get<OSDText>().Text = text;
                    label.Text = text;
                }
            }
            else
            {
                _labels[npcId] = new Label { ENpc = eNpc, ELabel = _createLabel(eNpc, text), Text = text };
                Trace(_dc, $"TALE overlay: label for NPC {npcId}");
            }
        }

        /*
         * Labels whose NPC is no longer materialised (or no longer alive).
         */
        List<int> gone = null;
        foreach (var (npcId, label) in _labels)
        {
            if (!seen.Contains(npcId) || !label.ENpc.IsAlive)
            {
                (gone ??= new()).Add(npcId);
            }
        }

        if (gone != null)
        {
            foreach (var npcId in gone)
            {
                _deleteLabel(_labels[npcId]);
                _labels.Remove(npcId);
            }
        }
    }


    private void _onLogicalFrame(object? sender, float dt)
    {
        _sinceRefresh += dt;
        if (_sinceRefresh < RefreshInterval) return;
        _sinceRefresh = 0f;
        _refresh();
    }


    protected override void OnModuleDeactivate()
    {
        _engine.OnLogicalFrame -= _onLogicalFrame;
        _deleteAllLabels();
    }


    protected override void OnModuleActivate()
    {
        _sinceRefresh = RefreshInterval;
        _engine.OnLogicalFrame += _onLogicalFrame;
    }
}
