using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using BepuPhysics;
using builtin.modules.satnav;
using builtin.modules.satnav.desc;
using engine.joyce;
using engine.joyce.components;
using engine.news;
using engine.physics;
using engine.physics.components;
using engine.quest.components;
using engine.world.components;
using static engine.Logger;
using Object = System.Object;

namespace engine.quest;


public class ToSomewhere : AModule
{
    /**
     * An internal name for the goal
     */
    public string Name { get; set; } = "Unnamed goal";

    /**
     * May be set to a parent entity this behaviour shall be attached
     * to.
     */
    public DefaultEcs.Entity ParentEntity { get; set; }

    /**
     * The quest entity that owns this navigation target.
     * When set, this ToSomewhere only shows its marker and route
     * when the owning quest is the currently followed quest.
     * When default (unset), legacy behavior applies: always show marker and route.
     */
    public DefaultEcs.Entity OwnerQuestEntity { get; set; }

    /**
     * Where, relative to its parent (or to the world) shall
     * the location be positioned?
     */
    public Vector3 RelativePosition { get; set; } = Vector3.Zero;

    /**
     * We react to collision with physics starting with this string.
     */
    public string SensitivePhysicsName { get; set; } = "";

    public float SensitiveRadius { get; set; } = 3f;

    /**
     * The map we shall render on
     */
    public uint MapCameraMask { get; set; } = 0x00800000;

    /**
     * What icon shall be used as target
     */
    public MapIcon.IconCode MapIcon { get; set; } = world.components.MapIcon.IconCode.Target0;

    /**
     * If I am supposed to create a visible target for this one.
     */
    public bool DoCreateVisibleTarget { get; set; } = true;

    public Action OnReachTarget = default;

    /**
     * We let ourselves know if we are not supposed to continue.
     */
    private bool _isStopped = false;
    private System.Threading.Timer _updateRouteTimer;

    /**
     * The route to the target.
     */
    private builtin.modules.satnav.Route _routeTarget;

    private IWaypoint _wStart = null;
    private IWaypoint _wTarget = null;

    /**
     * The specific visual marker of the mission.
     */
    private DefaultEcs.Entity _eMarker;
    private DefaultEcs.Entity _eMeshMarker;

    private DefaultEcs.Entity _eRouteParent;

    /*
     * The abstract mission target that physically shall be reached.
     */
    private DefaultEcs.Entity _eGoal;


    private static Lazy<engine.joyce.InstanceDesc> _jMeshGoal = new(
        () => InstanceDesc.CreateFromMatMesh(
            new MatMesh(
                new Material() { EmissiveTexture = I.Get<TextureCatalogue>().FindColorTexture(0xff888822) },
                engine.joyce.mesh.Tools.CreateCubeMesh($"goal mesh", 1f)
            ),
            400f
        )
    );


    private static Lazy<GoalMarkerSpinBehavior> _goalMarkerSpinBehavior = new(() => new GoalMarkerSpinBehavior());


    private void _onCollision(ContactEvent cev)
    {
        if (cev.ContactInfo.PropertiesB?.Name?.StartsWith(SensitivePhysicsName) ?? false)
        {
            /*
             * Guard against multiple physics callbacks firing while the player
             * overlaps the goal.  _isStopped is also set in OnModuleDeactivate.
             */
            if (_isStopped) return;
            _isStopped = true;

            Trace("Called onCollision of ToLocation.");

            /*
             * Queue everything on the main thread so that the full strategy
             * lifecycle (OnExit / OnDetach / DeactivateQuest) runs on the
             * logical thread, avoiding memory-visibility issues with
             * _activeStrategy and _questTarget fields.
             */
            _engine.QueueMainThreadAction(() =>
            {
                if (_eMeshMarker.IsAlive)
                {
                    _eMeshMarker.Get<engine.behave.components.Behavior>().Provider =
                        new GoalMarkerVanishBehavior();
                }

                OnReachTarget?.Invoke();
            });
        }
    }


    private void _deleteWaypointsLT()
    {
        if (_eRouteParent != default)
        {
            I.Get<HierarchyApi>().Delete(ref _eRouteParent);
        }
    }


    private void _destroyTargetInstanceLT()
    {
        if (_eGoal.IsAlive)
        {
             I.Get<HierarchyApi>().Delete(ref _eGoal);
        }
    }


    /**
     * Create the default target marker
     */
    private void _createTargetInstance(DefaultEcs.Entity eParent)
    {
        _eMarker = _engine.CreateEntity($"quest.goal {Name} marker");
        I.Get<TransformApi>().SetTransforms(_eMarker, true, 0x0000ffff, Quaternion.Identity, Vector3.Zero);
        I.Get<HierarchyApi>().SetParent(_eMarker, eParent);

        _eMeshMarker = _engine.CreateEntity($"quest.goal {Name} mesh marker");
        _eMeshMarker.Set(new engine.joyce.components.Instance3(_jMeshGoal.Value));
        I.Get<HierarchyApi>().SetParent(_eMeshMarker, _eMarker);
        I.Get<TransformApi>().SetTransforms(_eMeshMarker, true, 0x0000ffff, Quaternion.Identity,
            Vector3.Zero,
            new Vector3(SensitiveRadius, 3f, SensitiveRadius));
        _eMeshMarker.Set(
            new engine.behave.components.Behavior(_goalMarkerSpinBehavior.Value)
            {
                MaxDistance = 2000
            });

        DefaultEcs.Entity eMapMarker = _engine.CreateEntity($"quest.goal {Name} map marker");
        I.Get<HierarchyApi>().SetParent(eMapMarker, _eMarker);
        I.Get<TransformApi>().SetTransforms(eMapMarker, true,
            MapCameraMask, Quaternion.Identity, Vector3.Zero);

        eMapMarker.Set(new engine.world.components.MapIcon()
            { Code = MapIcon });
    }

    private Lazy<Mesh> _jMesh = new(() => joyce.mesh.Tools.CreateCubeMesh($"waypoint [idx]", 3f));



    private void _onJunctions(List<NavLane> listLanes)
    {
        if (_isStopped) return;
        _engine.QueueMainThreadAction(() =>
        {
            /*
             * Delete any old waypoints, if still there.
             */
            _deleteWaypointsLT();

            /*
             * Do not create new routes.
             */
            if (_isStopped) return;

            _eRouteParent = _engine.CreateEntity("routeparent");
            I.Get<TransformApi>().SetTransforms(_eRouteParent,
                true,
                MapCameraMask | 0x00000001,
                Quaternion.Identity, Vector3.Zero);

            var eWayPoint = _engine.CreateEntity($"waypoints");

            I.Get<HierarchyApi>().SetParent(eWayPoint, _eRouteParent);
            I.Get<TransformApi>().SetTransforms(eWayPoint,
                true,
                MapCameraMask | 0x00000001,
                Quaternion.Identity, -0.5f*Vector3.UnitY);

            var jMesh = joyce.Mesh.CreateListInstance("waypoints");
            int idx = 0;
            foreach (var nl in listLanes)
            {
                var v3Direction = nl.End.Position - nl.Start.Position;
                var vu3Right = Vector3.Normalize(new(v3Direction.Z, 0f, -v3Direction.X));

                joyce.mesh.Tools.AddQuadXYUV(jMesh,
                    nl.Start.Position+2f*vu3Right, -4f*vu3Right, v3Direction,
                    Vector2.Zero, Vector2.Zero, Vector2.Zero
                    );
            }
            var jInstanceDesc = InstanceDesc.CreateFromMatMesh(
                new MatMesh(I.Get<ObjectRegistry<Material>>().Get("nogame.characters.ToSomewhere.materials.waypoint"),
                    jMesh), 10000f);

            eWayPoint.Set(new Instance3(jInstanceDesc));

        });
    }



    private void _updateRoute(Object state)
    {
        if (_isStopped) return;

        Route routeTarget;
        lock (_lo)
        {
            routeTarget = _routeTarget;
        }

        if (routeTarget == null)
        {
            /*
             * Route creation failed earlier (e.g. player not ready).
             * Retry on the main thread.
             */
            _engine.QueueMainThreadAction(() =>
            {
                if (_isStopped) return;
                if (_tryCreateRouteLT())
                {
                    _routeTarget.Activate();
                    _routeTarget.Search(_onJunctions);
                }
            });
            return;
        }

        routeTarget.Search(_onJunctions);
    }


    private void _stopRoute()
    {
        Route routeTarget;
        Timer updateRouteTimer;
        lock (_lo)
        {
            routeTarget = _routeTarget;
            updateRouteTimer = _updateRouteTimer;
        }

        routeTarget?.Suspend();
        updateRouteTimer?.Dispose();
        _engine.QueueMainThreadAction(_deleteWaypointsLT);
    }


    private void _startRouteLT()
    {
        Route routeTarget;
        lock (_lo)
        {
            routeTarget = _routeTarget;
        }

        if (routeTarget != null)
        {
            routeTarget.Activate();
            routeTarget.Search(_onJunctions);
        }

        /*
         * Set up a periodic timer to update the route.
         * This also acts as a retry mechanism: if _tryCreateRouteLT
         * failed (e.g. player not available yet), subsequent timer
         * ticks will retry route creation and search.
         */
        _updateRouteTimer = new System.Threading.Timer(
            _updateRoute,
            this,
            7104, 7104);
    }


    private void _destroyRoute()
    {
        Route routeTarget;
        IWaypoint wTarget;
        IWaypoint wStart;
        lock (_lo)
        {
            routeTarget = _routeTarget;
            _routeTarget = null;
            wTarget = _wTarget;
            _wTarget = null;
            wStart = _wStart;
            _wStart = null;
        }

        routeTarget?.Dispose();
        wTarget?.Dispose();
        wStart?.Dispose();
    }


    /**
     * Try to create the route. Returns true on success, false if
     * preconditions (player entity, satnav module) are not met yet.
     */
    private bool _tryCreateRouteLT()
    {
        if (!_engine.Player.TryGet(out var ePlayer))
        {
            Trace("ToSomewhere: Player not available yet, deferring route creation.");
            return false;
        }

        try
        {
            if (ParentEntity != default)
            {
                _wTarget = new EntityWaypoint()
                {
                    Carrot = ParentEntity
                };
            }
            else
            {
                _wTarget = new StaticWaypoint()
                {
                    Location = RelativePosition
                };
            }

            _wStart = new PlayerWaypoint();

            lock (_lo)
            {
                _routeTarget = M<builtin.modules.satnav.Module>().CreateRoute(
                    _wStart, _wTarget);
            }

            return true;
        }
        catch (Exception e)
        {
            Warning($"ToSomewhere: Unable to create route: {e.Message}");
            return false;
        }
    }


    /**
     * Create goal is called from the main thread.
     */
    private void _createGoalLT()
    {
        BodyReference prefCylinder;
        BodyHandle phandleCylinder;

        _eGoal = _engine.CreateEntity($"goal {Name}");
        engine.physics.Object po;
        CollisionProperties collisionProperties = new() {
            Entity = _eGoal,
            Flags =
                engine.physics.CollisionProperties.CollisionFlags.IsDetectable
                | engine.physics.CollisionProperties.CollisionFlags.TriggersCallbacks,
            Name = Name,
            SolidLayerMask = CollisionProperties.Layers.QuestMarker,
            SensitiveLayerMask = CollisionProperties.Layers.Player
        };
        lock (_engine.Simulation)
        {
            var shape = I.Get<ShapeFactory>().GetCylinderShape(SensitiveRadius, 1000f);
            po = new engine.physics.Object(_engine, _eGoal, shape);
            prefCylinder = _engine.Simulation.Bodies.GetBodyReference(new BodyHandle(po.IntHandle));
        }

        po.CollisionProperties = collisionProperties;
        po.OnCollision = _onCollision;
        _eGoal.Set(new Body(po, prefCylinder));
        if (ParentEntity.IsAlive)
        {
            I.Get<HierarchyApi>().SetParent(_eGoal, ParentEntity);
        }

        I.Get<joyce.TransformApi>().SetTransforms(_eGoal, true, 0, Quaternion.Identity, RelativePosition);

        if (DoCreateVisibleTarget && _shouldShowMarker())
        {
            _createTargetInstance(_eGoal);
        }
    }


    private void _destroyGoal()
    {
        _engine.QueueMainThreadAction(() =>
        {
            _destroyTargetInstanceLT();
        });
    }


    /**
     * Returns true if this ToSomewhere should display its goal marker and start its route.
     * Legacy behavior (no OwnerQuestEntity): always true.
     * With OwnerQuestEntity: only true when the quest is currently followed.
     */
    private bool _shouldShowMarker()
    {
        if (OwnerQuestEntity == default) return true;
        var svc = I.TryGet<ISatnavService>();
        return svc == null || svc.IsFollowed(OwnerQuestEntity);
    }


    private void _handleQuestFollowed(Event ev)
    {
        if (OwnerQuestEntity == default || !OwnerQuestEntity.IsAlive) return;
        if (!OwnerQuestEntity.Has<QuestInfo>()) return;
        if (OwnerQuestEntity.Get<QuestInfo>().QuestId != ev.Code) return;

        // Our quest became the followed quest.
        _engine.QueueMainThreadAction(() =>
        {
            if (_isStopped) return;
            if (!_eMeshMarker.IsAlive && DoCreateVisibleTarget && _eGoal.IsAlive)
            {
                _createTargetInstance(_eGoal);
            }
        });

        // Start route if not already running.
        Route routeTarget;
        lock (_lo) { routeTarget = _routeTarget; }
        if (routeTarget == null)
        {
            _engine.QueueMainThreadAction(() =>
            {
                if (_isStopped) return;
                _tryCreateRouteLT();
                _startRouteLT();
            });
        }
    }


    private void _handleQuestUnfollowed(Event ev)
    {
        if (OwnerQuestEntity == default || !OwnerQuestEntity.IsAlive) return;
        if (!OwnerQuestEntity.Has<QuestInfo>()) return;
        if (OwnerQuestEntity.Get<QuestInfo>().QuestId != ev.Code) return;

        // Our quest was unfollowed — stop route and hide marker.
        _stopRoute();
        _destroyRoute();
        _engine.QueueMainThreadAction(() =>
        {
            if (_eMarker.IsAlive)
            {
                I.Get<HierarchyApi>().Delete(ref _eMarker);
            }
            _deleteWaypointsLT();
        });
    }


    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<builtin.modules.satnav.Module>()
    };


    protected override void OnModuleDeactivate()
    {
        _isStopped = true;
        _stopRoute();
        _destroyRoute();
        _destroyGoal();
    }


    protected override void OnModuleActivate()
    {
        I.Get<ObjectRegistry<Material>>().RegisterFactory("nogame.characters.ToSomewhere.materials.waypoint",
            name => new Material()
            {
                Texture = I.Get<TextureCatalogue>().FindColorTexture(0xffeeaa22)
            });

        if (OwnerQuestEntity != default)
        {
            Subscribe(QuestFollowedEvent.EVENT_TYPE, _handleQuestFollowed);
            Subscribe(QuestUnfollowedEvent.EVENT_TYPE, _handleQuestUnfollowed);
        }

        _engine.QueueMainThreadAction(() =>
        {
            _createGoalLT();
        });

        // Only start route navigation if this quest is followed (or no owner = legacy).
        if (_shouldShowMarker())
        {
            _engine.QueueMainThreadAction(() =>
            {
                _tryCreateRouteLT();
                _startRouteLT();
            });
        }
    }
}
