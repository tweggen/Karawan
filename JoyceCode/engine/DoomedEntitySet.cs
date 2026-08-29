using System;
using System.Collections.Generic;
using DefaultEcs;
using static engine.Logger;

namespace engine;

/**
 * The entities that have been asked to stop existing but have not been destroyed yet.
 *
 * Dooming is deliberately not destroying. engine.Engine drains this set at a point in
 * the logical frame where destroying entities is safe, and it skips that drain
 * altogether on every eighth frame (the frame that loads and purges world fragments
 * instead) and on any frame whose budget is already spent. A doomed entity therefore
 * stays alive, and stays visible to every system including BehaviorSystem, for at
 * least one more frame. That is the whole reason this is a set and not a list.
 *
 * Two things can ask for the same entity to go away without either being able to see
 * the other:
 *
 *  - engine.world.Fragment.RemoveFragmentEntities dooms everything its fragment owns
 *    when the fragment unloads, while a behaviour riding on one of those entities
 *    dooms itself when its own timer expires. Creation and ownership are separate
 *    concepts in this engine on purpose, so there is no single place that could
 *    arbitrate between the two.
 *  - nogame.inv.coin.Behavior dooms the coin from OnCollision. Two contacts in one
 *    frame, or contacts on consecutive frames before the drain runs, are ordinary
 *    physics and not something a collision handler can reasonably deduplicate.
 *
 * A repeated doom is therefore a legitimate outcome of uncoordinated owners, not a
 * programming error, and this class answers it by being idempotent.
 *
 * What it does have to guarantee is that the entity is destroyed exactly ONCE, and
 * that guarantee is not cosmetic. DefaultEcs recycles entity ids on dispose
 * (World.On(EntityDisposedMessage) calls _entityIdDispenser.ReleaseInt), and
 * Entity.Dispose looks at neither IsAlive nor Version - it just publishes
 * EntityDisposingMessage for the id. A second Dispose therefore tears down whatever
 * now holds that id and releases it a second time, after which the dispenser hands
 * one id out to two live entities. Deduplicating here is what prevents that, so the
 * set exists in every build configuration; it used to be DEBUG-only, with release
 * builds keeping an undeduplicated list of lists and disposing twice.
 */
public class DoomedEntitySet
{
    private static readonly engine.Dc _dc = engine.Dc.Engine;

    private readonly object _lo = new();
    private readonly HashSet<Entity> _setDoomed = new();


    public int Count
    {
        get
        {
            lock (_lo)
            {
                return _setDoomed.Count;
            }
        }
    }


    public bool Contains(Entity entity)
    {
        lock (_lo)
        {
            return _setDoomed.Contains(entity);
        }
    }


    /**
     * Doom the given entity.
     *
     * @return true if this call is what doomed it, false if it already was doomed or
     *         cannot be doomed at all.
     */
    public bool Add(Entity entity)
    {
        if (!entity.IsAlive)
        {
            /*
             * This is not the two-owners race and must not be quietly tolerated: the
             * Entity struct we were handed refers to something that already has been
             * destroyed, so its id may well denote a different, live entity by now.
             * Accepting it would schedule that innocent entity for destruction.
             */
#if DEBUG
            ErrorThrow<ArgumentException>($"Tried to kill an entity {entity} that has not been alive anymore.");
#else
            Error($"Tried to kill an entity {entity} that has not been alive anymore.");
#endif
            return false;
        }

        lock (_lo)
        {
            if (!_setDoomed.Add(entity))
            {
                /*
                 * Trace and not Warning: with the drain deferred by design, a second
                 * owner arriving before the first destruction is expected traffic,
                 * and a warning that fires during normal play is a warning nobody
                 * reads. The invariant that would deserve a warning - destroying it
                 * twice - is the one this return prevents.
                 */
                Trace(_dc, $"Entity {entity} already was doomed before.");
                return false;
            }
        }

        return true;
    }


    public void AddRange(in IEnumerable<Entity> entities)
    {
        foreach (var entity in entities)
        {
            Add(entity);
        }
    }


    /**
     * Hand out everything doomed so far and forget it, so that the caller can destroy
     * each of them exactly once.
     *
     * @return false if nothing is doomed, in which case nothing is allocated either.
     */
    public bool TryDrain(out List<Entity> listDoomed)
    {
        lock (_lo)
        {
            if (0 == _setDoomed.Count)
            {
                listDoomed = null;
                return false;
            }

            listDoomed = new(_setDoomed);
            _setDoomed.Clear();
            return true;
        }
    }
}
