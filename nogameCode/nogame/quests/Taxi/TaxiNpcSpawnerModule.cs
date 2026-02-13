using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BepuPhysics;
using builtin.tools;
using engine;
using engine.joyce;
using engine.joyce.components;
using engine.physics;
using engine.physics.components;
using engine.quest;
using engine.streets;
using engine.world;
using engine.world.components;
using static engine.Logger;

namespace nogame.quests.Taxi;


/// <summary>
/// Manages taxi NPC markers across the game world.
/// Spawns ~1 in 10 quarters per cluster with a spinning marker
/// that players can touch to start taxi quests.
/// </summary>
public class TaxiNpcSpawnerModule : AModule
{
    private class NpcRecord
    {
        public DefaultEcs.Entity MarkerEntity;
        public ClusterDesc ClusterDesc;
        public Quarter Quarter;
        public Vector3 Position;
    }

    private readonly List<NpcRecord> _npcRecords = new();
    private bool _isTaxiQuestActive = false;
    private int _replacementCounter = 0;

    private const uint _mapCameraMask = 0x00800000;
    private const float _sensitiveRadius = 3f;
    private const string _questId = "nogame.quests.Taxi.Quest";

    private static readonly Lazy<InstanceDesc> _jMeshNpc = new(
        () => InstanceDesc.CreateFromMatMesh(
            new MatMesh(
                new Material() { EmissiveTexture = I.Get<TextureCatalogue>().FindColorTexture(0xff22aa88) },
                engine.joyce.mesh.Tools.CreateCubeMesh("taxi npc mesh", 1f)
            ),
            400f
        )
    );

    private static readonly Lazy<GoalMarkerSpinBehavior> _spinBehavior =
        new(() => new GoalMarkerSpinBehavior());


    private void _spawnSingleNpcLT(ClusterDesc clusterDesc, Quarter quarter, RandomSource rnd)
    {
        Vector3 v3Local = quarter.GetCenterPoint3();
        Vector3 v3World = (clusterDesc.Pos + v3Local) with { Y = clusterDesc.AverageHeight + 2f };

        var eNpcRoot = _engine.CreateEntity($"taxi.npc {clusterDesc.Name}");

        engine.physics.Object po;
        BodyReference prefCylinder;
        CollisionProperties collisionProperties = new()
        {
            Entity = eNpcRoot,
            Flags =
                CollisionProperties.CollisionFlags.IsDetectable
                | CollisionProperties.CollisionFlags.TriggersCallbacks,
            Name = $"taxi.npc.{clusterDesc.Name}",
            SolidLayerMask = CollisionProperties.Layers.QuestMarker,
            SensitiveLayerMask = CollisionProperties.Layers.Player
        };
        lock (_engine.Simulation)
        {
            var shape = I.Get<ShapeFactory>().GetCylinderShape(_sensitiveRadius, 1000f);
            po = new engine.physics.Object(_engine, eNpcRoot, shape);
            prefCylinder = _engine.Simulation.Bodies.GetBodyReference(new BodyHandle(po.IntHandle));
        }

        var record = new NpcRecord
        {
            MarkerEntity = eNpcRoot,
            ClusterDesc = clusterDesc,
            Quarter = quarter,
            Position = v3World
        };

        po.CollisionProperties = collisionProperties;
        po.OnCollision = (cev) =>
        {
            if (cev.ContactInfo.PropertiesB?.Name?.StartsWith("nogame.playerhover.") ?? false)
            {
                _onNpcTouched(record);
            }
        };

        eNpcRoot.Set(new Body(po, prefCylinder));
        I.Get<TransformApi>().SetTransforms(eNpcRoot, true, 0, Quaternion.Identity, v3World);

        // Visual marker container
        var eMarker = _engine.CreateEntity($"taxi.npc marker {clusterDesc.Name}");
        I.Get<TransformApi>().SetTransforms(eMarker, true, 0x0000ffff, Quaternion.Identity, Vector3.Zero);
        I.Get<HierarchyApi>().SetParent(eMarker, eNpcRoot);

        // Spinning mesh cube
        var eMesh = _engine.CreateEntity($"taxi.npc mesh {clusterDesc.Name}");
        eMesh.Set(new Instance3(_jMeshNpc.Value));
        I.Get<HierarchyApi>().SetParent(eMesh, eMarker);
        I.Get<TransformApi>().SetTransforms(eMesh, true, 0x0000ffff, Quaternion.Identity,
            Vector3.Zero, new Vector3(_sensitiveRadius, 3f, _sensitiveRadius));
        eMesh.Set(new engine.behave.components.Behavior(_spinBehavior.Value) { MaxDistance = 2000 });

        // Map icon
        var eMapIcon = _engine.CreateEntity($"taxi.npc map {clusterDesc.Name}");
        I.Get<HierarchyApi>().SetParent(eMapIcon, eMarker);
        I.Get<TransformApi>().SetTransforms(eMapIcon, true, _mapCameraMask, Quaternion.Identity, Vector3.Zero);
        eMapIcon.Set(new MapIcon() { Code = MapIcon.IconCode.Target0 });

        lock (_lo)
        {
            _npcRecords.Add(record);
        }
    }


    private void _onNpcTouched(NpcRecord record)
    {
        _engine.QueueMainThreadAction(() =>
        {
            lock (_lo)
            {
                if (_isTaxiQuestActive) return;
                if (!_npcRecords.Contains(record)) return;
                _isTaxiQuestActive = true;
                _npcRecords.Remove(record);
            }

            if (record.MarkerEntity.IsAlive)
            {
                I.Get<HierarchyApi>().Delete(ref record.MarkerEntity);
            }

            _ = I.Get<QuestFactory>().TriggerQuest(_questId, true);
        });
    }


    private void _onClusterCompleted(engine.news.Event ev)
    {
        var clusterList = I.Get<ClusterList>();
        ClusterDesc clusterDesc = null;
        foreach (var cd in clusterList.GetClusterList())
        {
            if (cd.Name == ev.Code)
            {
                clusterDesc = cd;
                break;
            }
        }

        if (clusterDesc == null) return;

        var rnd = new RandomSource(clusterDesc.Name + "taxinpc");
        var quarters = clusterDesc.QuarterStore().GetQuarters();
        if (quarters == null || quarters.Count == 0) return;

        _engine.QueueMainThreadAction(() =>
        {
            foreach (var quarter in quarters)
            {
                if (quarter.IsInvalid()) continue;
                if (rnd.GetFloat() < 0.1f)
                {
                    _spawnSingleNpcLT(clusterDesc, quarter, rnd);
                }
            }
        });
    }


    private void _onQuestDeactivated(engine.news.Event ev)
    {
        if (ev.Code != _questId) return;

        lock (_lo)
        {
            _isTaxiQuestActive = false;
        }

        var clusterList = I.Get<ClusterList>();
        var allClusters = clusterList.GetClusterList();
        if (allClusters.Count == 0) return;

        var rnd = new RandomSource($"taxinpc.replace.{_replacementCounter++}");

        HashSet<Quarter> occupiedQuarters;
        lock (_lo)
        {
            occupiedQuarters = new HashSet<Quarter>(_npcRecords.Select(r => r.Quarter));
        }

        int clusterStartIdx = rnd.GetInt(allClusters.Count);
        for (int ci = 0; ci < allClusters.Count; ci++)
        {
            var cd = allClusters[(clusterStartIdx + ci) % allClusters.Count];
            var quarters = cd.QuarterStore().GetQuarters();
            if (quarters == null || quarters.Count == 0) continue;

            int quarterStartIdx = rnd.GetInt(quarters.Count);
            for (int qi = 0; qi < quarters.Count; qi++)
            {
                var q = quarters[(quarterStartIdx + qi) % quarters.Count];
                if (q.IsInvalid()) continue;
                if (!occupiedQuarters.Contains(q))
                {
                    _engine.QueueMainThreadAction(() => _spawnSingleNpcLT(cd, q, rnd));
                    return;
                }
            }
        }
    }


    private void _destroyAllNpcsLT()
    {
        List<NpcRecord> records;
        lock (_lo)
        {
            records = new List<NpcRecord>(_npcRecords);
            _npcRecords.Clear();
        }

        foreach (var record in records)
        {
            if (record.MarkerEntity.IsAlive)
            {
                var e = record.MarkerEntity;
                I.Get<HierarchyApi>().Delete(ref e);
            }
        }
    }


    protected override void OnModuleDeactivate()
    {
        _engine.QueueMainThreadAction(_destroyAllNpcsLT);
    }


    protected override void OnModuleActivate()
    {
        Subscribe(ClusterCompletedEvent.EVENT_TYPE, _onClusterCompleted);
        Subscribe(QuestDeactivatedEvent.EVENT_TYPE, _onQuestDeactivated);
    }
}
