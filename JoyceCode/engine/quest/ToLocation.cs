using System;
using System.Diagnostics;
using System.Numerics;
using BepuPhysics;
using engine.joyce;
using engine.joyce.components;
using engine.physics;
using engine.physics.components;
using static engine.Logger;

namespace engine.quest;


/**
 * Create a quest of type "ToLocation"
 *
 * Encapsulates the actions to create the entities involved
 */
public class ToLocation : engine.world.IOperator
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
     * If I am supposed to create a visible target for this one.
     */
    public bool DoCreateVisibleTarget { get; set; } = true;


    private static Lazy<engine.joyce.InstanceDesc> _jMeshGoal = new(
        () => InstanceDesc.CreateFromMatMesh(
            new MatMesh(
                new Material() { AlbedoColor = 0xff4444cc },
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
        }
    }


    /**
     * Create the default target marker
     */
    private void _createTargetInstance(engine.Engine e, DefaultEcs.Entity eParent)
    {
        DefaultEcs.Entity eMarker = e.CreateEntity("quest.goal {Name} marker");
        eMarker.Set(_jMeshGoal.Value);
        I.Get<TransformApi>().SetTransforms(eMarker, true, 0x000000ff, Quaternion.Identity, Vector3.Zero);
        I.Get<HierarchyApi>().SetParent(eMarker, eParent);
        eMarker.Set(_goalMarkerSpinBehavior.Value);
    }
    
    
    public void OperatorApply(Engine e)
    {
        BodyReference prefCylinder;
        BodyHandle phandleCylinder;

        DefaultEcs.Entity eGoal = e.CreateEntity($"goal {Name}");

        lock (e.Simulation)
        {
            phandleCylinder = e.Simulation.Bodies.Add(
                BodyDescription.CreateKinematic(
                    new Vector3(0f, 0f, 0f), // infinite mass, this is a kinematic object.
                    new BepuPhysics.Collidables.CollidableDescription(
                        physics.ShapeFactory.GetCylinderShape(3f, e),
                        0.1f),
                    new BodyActivityDescription(0.01f)
                )
            );

            prefCylinder = e.Simulation.Bodies.GetBodyReference(phandleCylinder);
            
            /*
             * Position will be set by setup kinetics system.
             */
        }
        
        var poCylinder = new engine.physics.Object(eGoal, phandleCylinder)
        {
            CollisionProperties = new CollisionProperties()
            {
                Entity = eGoal, 
                Flags = 
                    engine.physics.CollisionProperties.CollisionFlags.IsDetectable
                    |engine.physics.CollisionProperties.CollisionFlags.TriggersCallbacks,
                Name = Name,
            }, 
            OnCollision = _onCollision
        };

        eGoal.Set(new Body(poCylinder, prefCylinder));
        I.Get<joyce.TransformApi>().SetPosition(eGoal, RelativePosition);

        if (DoCreateVisibleTarget)
        {
            _createTargetInstance(e, eGoal);
        }
    }
}
