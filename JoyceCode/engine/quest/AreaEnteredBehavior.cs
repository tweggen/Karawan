
using DefaultEcs;
using engine.behave;
using engine.physics;
using BepuPhysics;
using System.Numerics;
using static engine.Logger;

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
public class AreaEnteredBehavior : IBehavior
{
    private engine.Engine _engine;
    private BodyReference _prefCylinder;
    private engine.physics.Object _poCylinder;
    private DefaultEcs.Entity _eGoal;


    public void OnCollision(ContactEvent cev)
    {
        Trace("Something collided with me.");
    }

    public void Behave(in Entity entity, float dt)
    {
    }

    public void Sync(in Entity entity)
    {
    }

    public void OnDetach(in Entity entity)
    {
    }

    public void OnAttach(in Engine engine0, in Entity entity)
    {
    }
}
