using System;
using System.Numerics;
using BepuPhysics;
using engine.joyce;
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
     * The specific visual marker of the mission.
     */
    private DefaultEcs.Entity _eMarker;

    private DefaultEcs.Entity _eMeshMarker;
    
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


    public override void ModuleDeactivate()
    {
        _engine.RemoveModule(this);
        _destroyGoal();
        base.ModuleDeactivate();
    }
    

    public override void ModuleActivate()
    {
        base.ModuleActivate();
        _engine.QueueMainThreadAction(() =>
        {
            _createGoal();
        });
        _engine.AddModule(this);
    }
}
