using System;
using System.Collections.Generic;
using System.Numerics;
using BepuPhysics;
using builtin.modules.satnav;
using builtin.modules.satnav.desc;
using engine.joyce;
using engine.joyce.components;
using engine.physics;
using engine.physics.components;
using static engine.Logger;

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
     * If I am supposed to create a visible target for this one.
     */
    public bool DoCreateVisibleTarget { get; set; } = true;
    
    public Action OnReachTarget = default;


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
             * At this point we can call whatever has been reached.
             */
            Trace("Called onCollision of ToLocation.");
            _eMeshMarker.Get<engine.behave.components.Behavior>().Provider = new GoalMarkerVanishBehavior();
            if (OnReachTarget != default)
            {
                OnReachTarget();
                _destroyTargetInstance();
            }
        }
    }


    private void _destroyTargetInstance()
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
            { Code = engine.world.components.MapIcon.IconCode.Target0 });
    }

    private Lazy<Mesh> _jMesh = new(() => joyce.mesh.Tools.CreateCubeMesh($"waypoint [idx]", 3f));



    private void _onJunctions(List<NavJunction> listJunctions)
    {
        _engine.QueueMainThreadAction(() =>
        {
            if (_eRouteParent != default)
            {
                I.Get<HierarchyApi>().Delete(ref _eRouteParent);
            }

            _eRouteParent = _engine.CreateEntity("routeparent");
            int idx = 0;
            foreach (var nj in listJunctions)
            {
                var eWayPoint = _engine.CreateEntity($"waypoint {idx}");
                I.Get<TransformApi>().SetTransforms(eWayPoint,
                    true,
                    MapCameraMask | 0x00000001,
                    Quaternion.Identity, nj.Position + 3*Vector3.UnitY);

                var jInstanceDesc = InstanceDesc.CreateFromMatMesh(
                    new MatMesh(I.Get<ObjectRegistry<Material>>().Get("nogame.characters.ToSomewhere.materials.waypoint"),
                        _jMesh.Value), 500f);
                eWayPoint.Set(new Instance3(jInstanceDesc));

                idx++;
            }
            
        });
    }
    
    
    private void _stopRoute()
    {
        _routeTarget.Suspend();
    }
    

    private void _startRoute()
    {
        _routeTarget.Activate();
        _routeTarget.Search(_onJunctions);
    }
    

    private void _destroyRoute()
    {
        _routeTarget.Dispose();
        _routeTarget = null;
        _wTarget.Dispose();
        _wTarget = null;
        _wStart.Dispose();
        _wStart = null;
    }
    
    
    private void _createRoute()
    {
        if (!_engine.TryGetPlayerEntity(out var ePlayer))
        {
            ErrorThrow<InvalidOperationException>("No player defined currently.");
        }

        /*
         * Create a route from the player to the target.
         */
        _wTarget = new StaticWaypoint()
        {
            Location = _eGoal.Get<Transform3ToWorld>().Matrix.Translation
        };

        _wStart = new EntityWaypoint()
        {
            Carrot = ePlayer
        };
        
        /*
         * Finally, create a route from it.
         */
        _routeTarget = M<builtin.modules.satnav.Module>().CreateRoute(
            _wStart, _wTarget);
    }


    /**
     * Create goal is called from the main thread.
     */
    private void _createGoal()
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
        };
        lock (_engine.Simulation)
        {
            var shape = I.Get<ShapeFactory>().GetCylinderShape(SensitiveRadius);
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

        if (DoCreateVisibleTarget)
        {
            _createTargetInstance(_eGoal);
        }
    }
    

    private void _destroyGoal()
    {
        _destroyTargetInstance();
    }
    
    
    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<builtin.modules.satnav.Module>()
    };


    public override void ModuleDeactivate()
    {
        _engine.RemoveModule(this);
        _stopRoute();
        _destroyRoute();
        _destroyGoal();
        base.ModuleDeactivate();
    }
    

    public override void ModuleActivate()
    {
        base.ModuleActivate();

        I.Get<ObjectRegistry<Material>>().RegisterFactory("nogame.characters.ToSomewhere.materials.waypoint",
            name => new Material()
            {
                Texture = I.Get<TextureCatalogue>().FindColorTexture(0xff22aaee)
            });

        
        _engine.QueueMainThreadAction(() =>
        {
            _createGoal();
        });
        _engine.QueueMainThreadAction(() =>
        {
            _createRoute();
            _startRoute();
        });
        _engine.AddModule(this);
    }
}
