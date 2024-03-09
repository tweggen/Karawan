using DefaultEcs;
using engine;
using engine.behave;
using engine.news;

namespace Joyce.builtin.tools;


/**
 * Attached to an entity with a fragment id:
 *
 * Take care that a certain number of entities of some sort exist on this
 * fragment, possibly bounded.
 *
 * - look for entities in the area
 * - determine the number of entities that need to be brought to life.
 * - look for a number of places to put the entities in
 * - bring a number of entities to life.
 * - repeat.
 *
 * To save computing time, we split our own behavior into a couple of different phases.
 * Also, the most precious resource is the access the logical thread.
 * Therefore:
 * - scan at least after ScanInterval seconds for the number of entities
 *   within the given constraints (logical thread required)
 * - inside any thread
 *   - chose a start point
 *   - instantiate model
 *   - prepare physics (locked on Simulation)
 * - schedule setup entity.
 */
public class SpawnBehavior : ABehavior
{
    /**
     * The desired target number of entities.
     */
    public uint DesiredEntities { get; set; }

    /**
     * Scan at least after this amount of seconds.
     */
    public float ScanInterval { get; set; } = 0.9f;
    
    
    public virtual void Behave(in Entity entity, float dt)
    {
        base.Behave(entity, dt);
    }

    
    public override void OnDetach(in Entity entity)
    {
        if (null != DetachEventType)
        {
            I.Get<EventQueue>().Push(
                new engine.news.Event(
                    engine.news.Event.BEHAVIOR_LOST_CUSTOM_EVENT + DetachEventType,
                    DetachEventCode));
        }
        base.OnDetach(entity);
    }

    public override void OnAttach(in Engine engine0, in Entity entity)
    {
        base.OnAttach(engine0, entity);
    }
}