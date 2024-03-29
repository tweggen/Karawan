using System;
using System.Numerics;
using BepuPhysics;
using engine.joyce;
using engine.physics;
using engine.physics.components;
using static engine.Logger;

namespace engine.quest;


public class ToSomewhere : engine.world.IOperator
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

    /**
     * The map we shall render on
     */
    public uint MapCameraMask { get; set; } = 0x00800000;


    /**
     * If I am supposed to create a visible target for this one.
     */
    public bool DoCreateVisibleTarget { get; set; } = true;
    

    public Action OnReachTarget = default;


    private DefaultEcs.Entity _eMarker; 
        
    private static Lazy<engine.joyce.InstanceDesc> _jMeshGoal = new(
        () => InstanceDesc.CreateFromMatMesh(
            new MatMesh(
                new Material() { EmissiveColor = 0xff888822, AlbedoColor = 0xff0000ff },
                engine.joyce.mesh.Tools.CreateCubeMesh($"goal mesh", 3f)
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
            _eMarker.Get<engine.behave.components.Behavior>().Provider = new GoalMarkerVanishBehavior();
            if (OnReachTarget != default)
            {
                OnReachTarget();
            }
        }
    }


    /**
     * Create the default target marker
     */
    private void _createTargetInstance(engine.Engine e, DefaultEcs.Entity eParent)
    {
        _eMarker = e.CreateEntity($"quest.goal {Name} marker");
        _eMarker.Set(new engine.joyce.components.Instance3(_jMeshGoal.Value));
        I.Get<TransformApi>().SetTransforms(_eMarker, true, 0x0000ffff, Quaternion.Identity, Vector3.Zero);
        I.Get<HierarchyApi>().SetParent(_eMarker, eParent);
        _eMarker.Set(
            new engine.behave.components.Behavior(_goalMarkerSpinBehavior.Value)
            {
                MaxDistance = 2000f
            });
        DefaultEcs.Entity eMapMarker = e.CreateEntity($"quest goal {Name} map marker");
        I.Get<HierarchyApi>().SetParent(eMapMarker, _eMarker); 
        I.Get<TransformApi>().SetTransforms(eMapMarker, true, 
            MapCameraMask, Quaternion.Identity, Vector3.Zero);

        eMapMarker.Set(new engine.world.components.MapIcon()
            { Code = engine.world.components.MapIcon.IconCode.Target0 });
    }


    /**
     * Create goal is called from the main thread.
     */
    private void _createGoal(Engine e)
    {
        BodyReference prefCylinder;
        BodyHandle phandleCylinder;

        DefaultEcs.Entity eGoal = e.CreateEntity($"goal {Name}");
        engine.physics.Object po;
        CollisionProperties collisionProperties = new() {
            Entity = eGoal,
            Flags =
                engine.physics.CollisionProperties.CollisionFlags.IsDetectable
                | engine.physics.CollisionProperties.CollisionFlags.TriggersCallbacks,
            Name = Name,
        };
        lock (e.Simulation)
        {
            var shape = I.Get<ShapeFactory>().GetCylinderShape(3f);
            po = new engine.physics.Object(e, eGoal, shape);
            prefCylinder = e.Simulation.Bodies.GetBodyReference(new BodyHandle(po.IntHandle));
        }

        po.CollisionProperties = collisionProperties;
        po.OnCollision = _onCollision;
        eGoal.Set(new Body(po, prefCylinder));

        var apiTransform = I.Get<joyce.TransformApi>();
        apiTransform.SetTransforms(eGoal, true, 0, Quaternion.Identity, RelativePosition);

        if (DoCreateVisibleTarget)
        {
            _createTargetInstance(e, eGoal);
        }
    }
    
    
    public virtual void OperatorApply()
    {
        var e = I.Get<Engine>();
        e.QueueMainThreadAction(() =>
        {
            _createGoal(e);
        });
    }
}