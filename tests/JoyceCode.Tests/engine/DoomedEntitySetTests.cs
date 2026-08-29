using System;
using System.Collections.Generic;
using DefaultEcs;
using engine;
using Xunit;

namespace JoyceCode.Tests.engine;

/**
 * Regression tests for the crash
 *
 *   System.ArgumentException: Entity Entity 1:1605.0 already was doomed before.
 *     at engine.Engine.AddDoomedEntity(Entity entity)
 *     at builtin.tools.AutoRemoveBehavior.Behave(Entity& entity, Single dt)
 *
 * engine.Engine only drains its doomed set on frames where
 * (_frameNumber &amp; 7) != 0 and more than 5 ms of the budget is left, so a doomed
 * entity survives at least one more frame and keeps being ticked by BehaviorSystem
 * in the meantime. Anything that dooms it again in that window - the same behaviour
 * on the next tick, a fragment unloading, a second physics contact - used to throw.
 *
 * These exercise DoomedEntitySet directly rather than through engine.Engine, whose
 * constructor registers about fifteen services into the process-global I container
 * and would throw "Already registered" against whichever other test class in this
 * assembly got there first (see TestContainer for that hazard). A plain
 * DefaultEcs.World needs no container at all and gives us real entities, real ids
 * and real id recycling, which is what the exactly-once guarantee is about.
 */
public class DoomedEntitySetTests
{
    /**
     * The reported crash, in the order it actually happens: doom, no drain because
     * this was a fragment frame, doom again on the next tick.
     */
    [Fact]
    public void SecondDoomBeforeTheDrainIsIdempotent()
    {
        using World world = new();
        Entity e = world.CreateEntity();

        DoomedEntitySet doomed = new();

        Assert.True(doomed.Add(e));

        /*
         * No TryDrain here on purpose - this is the eighth frame.
         */

        Assert.False(doomed.Add(e));
        Assert.Equal(1, doomed.Count);
        Assert.True(e.IsAlive);
    }


    /**
     * The point of the deduplication: however many owners doom it, it is destroyed
     * once. A second Dispose would publish EntityDisposingMessage for an id the
     * dispenser has already handed back out.
     */
    [Fact]
    public void TwoOwnersDoomingTheSameEntityDestroyItOnce()
    {
        using World world = new();
        Entity eShared = world.CreateEntity();
        Entity eFragmentOnly = world.CreateEntity();

        DoomedEntitySet doomed = new();

        /*
         * The behaviour dooms itself, then the fragment that owns it unloads and
         * dooms everything it owns, this entity among them.
         */
        doomed.Add(eShared);
        doomed.AddRange(new List<Entity> { eFragmentOnly, eShared });

        Assert.True(doomed.TryDrain(out var listDoomed));
        Assert.Equal(2, listDoomed.Count);
        Assert.Single(listDoomed, e => e == eShared);

        foreach (var e in listDoomed)
        {
            e.Dispose();
        }

        Assert.False(eShared.IsAlive);
        Assert.False(eFragmentOnly.IsAlive);
    }


    /**
     * What double destruction would actually cost. DefaultEcs releases an entity id
     * back to its dispenser on dispose and Entity.Dispose checks neither IsAlive nor
     * Version, so a stale Entity struct names a live stranger once the id has been
     * reused. Dooming it must be refused rather than scheduled.
     */
    [Fact]
    public void AStaleEntityWhoseIdWasRecycledIsRefused()
    {
        using World world = new();
        Entity eOld = world.CreateEntity();

        DoomedEntitySet doomed = new();
        doomed.Add(eOld);
        Assert.True(doomed.TryDrain(out var listDoomed));
        foreach (var e in listDoomed)
        {
            e.Dispose();
        }

        Entity eNew = world.CreateEntity();
        Assert.Equal(eOld.GetId(), eNew.GetId());
        Assert.NotEqual(eOld, eNew);

        Assert.Throws<ArgumentException>(() => doomed.Add(eOld));

        Assert.Equal(0, doomed.Count);
        Assert.False(doomed.Contains(eNew));
        Assert.True(eNew.IsAlive);
    }


    /**
     * Dooming an entity again AFTER it has been drained but before it was disposed
     * is the same race one drain later, and must still not throw.
     */
    [Fact]
    public void DrainingForgetsSoTheEntityCanBeDoomedAgainWhileStillAlive()
    {
        using World world = new();
        Entity e = world.CreateEntity();

        DoomedEntitySet doomed = new();
        doomed.Add(e);
        Assert.True(doomed.TryDrain(out _));

        Assert.False(doomed.TryDrain(out var listEmpty));
        Assert.Null(listEmpty);

        Assert.True(doomed.Add(e));
        Assert.Equal(1, doomed.Count);
    }


    /**
     * Concurrency: Fragment.RemoveFragmentEntities and a behaviour's self-doom reach
     * this from different call paths, so the set carries its own lock. Whoever wins,
     * exactly one caller may be told it did the dooming.
     */
    [Fact]
    public void ConcurrentDoomsOfOneEntityElectExactlyOneWinner()
    {
        using World world = new();
        Entity e = world.CreateEntity();

        DoomedEntitySet doomed = new();

        int nWinners = 0;
        System.Threading.Tasks.Parallel.For(0, 64, _ =>
        {
            if (doomed.Add(e))
            {
                System.Threading.Interlocked.Increment(ref nWinners);
            }
        });

        Assert.Equal(1, nWinners);
        Assert.Equal(1, doomed.Count);
    }
}
