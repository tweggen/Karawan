using BepuPhysics;
using System.Numerics;

namespace engine.quest;


/**
 * Defines a quest to go to a particular location.
 *
 * Creating this quest involves creating an entity representing the quest.
 *
 * The entity may place various ancillary entities that would disappear
 * after the quest, such as a collision shape to identify the target
 * location.
 */
public class ToLocation : AModule
{
    public Vector3 TargetLocation { get; set; }

    private BodyReference _prefCylinder;

    public override void ModuleDeactivate()
    {
        _engine.RemoveModule(this);
        base.ModuleDeactivate();
    }
    
    
    public override void ModuleActivate(Engine engine0)
    {
        base.ModuleActivate(engine0);
        engine0.AddModule(this);

        lock (_engine.Simulation)
        {
            BodyHandle phandleCylinder = _engine.Simulation.Bodies.Add(
                BodyDescription.CreateKinematic(
                    new Vector3(0f, 0f, 0f), // infinite mass, this is a kinematic object.
                    new BepuPhysics.Collidables.CollidableDescription(
                        engine.physics.ShapeFactory.GetCylinderShape(3f, _engine),
                        0.1f),
                    new BodyActivityDescription(0.01f)
                )
            );
            _prefCylinder = _engine.Simulation.Bodies.GetBodyReference(phandleCylinder);
        }
    }
}
