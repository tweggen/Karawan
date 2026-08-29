using System;
using System.IO;
using engine.physics;
using Xunit;

namespace JoyceCode.Tests.engine.physics;


/**
 * Friction is a property of the bodies now, not a constant of the world.
 *
 * It was 1f for every pair in the simulation, under a comment inherited from the Bepu
 * demo the callbacks were copied from that says so ("we'll use the same settings for
 * all pairs"). 1.0 is a wheels-on-asphalt number and the player's ship is a hover
 * vehicle with nothing to grip with; pressed onto a surface by its own hover loop it
 * resisted tangentially with more force than its engine could produce and simply
 * stopped.
 *
 * The actual contact resolution needs a running simulation and is not testable here.
 * What is testable is that the world's default did not move, that a body which asks to
 * be slippery gets to be, and that the two sites this depends on still say what they
 * are supposed to.
 */
public class PairFrictionTests
{
    /**
     * The whole point of doing this per body: nothing that did not ask for anything
     * changes. Every NPC, every piece of debris, every static in the world keeps the
     * coefficient it has always been resolved with.
     */
    [Fact]
    public void AnythingThatDoesNotAskKeepsTheWorldsOldFriction()
    {
        Assert.Equal(1f, CollisionProperties.DefaultFriction, 4);
        Assert.Equal(1f, new CollisionProperties().Friction, 4);
    }


    /**
     * The lower of the two wins, so a body that declares itself slippery is slippery
     * against everything.
     *
     * The alternative blends - average, or the geometric mean physical materials are
     * usually combined with - let the SURFACE argue, and the surface a hover ship most
     * needs to slide off is a road, which is exactly the one that would be given a high
     * coefficient of its own.
     */
    [Fact]
    public void TheSlipperierBodyDecidesThePair()
    {
        Assert.Equal(0.05f, CollisionProperties.CombineFriction(0.05f, 1f), 4);
        Assert.Equal(0.05f, CollisionProperties.CombineFriction(1f, 0.05f), 4);
        Assert.Equal(1f, CollisionProperties.CombineFriction(1f, 1f), 4);
        Assert.Equal(0.2f, CollisionProperties.CombineFriction(0.4f, 0.2f), 4);
    }


    private static string _repoRoot()
    {
        string root = global::engine.GameRoot.PathTo("JoyceCode");
        Assert.False(String.IsNullOrEmpty(root), "could not locate the checkout");

        return Path.GetFullPath(Path.Combine(root, ".."));
    }


    /**
     * The two ends of the mechanism, scanned, because neither can be reached from a
     * test and both fail silently.
     *
     * A hardcoded coefficient in the callback puts the world back to one friction for
     * everything and nothing anywhere says so - it is exactly the state this started
     * in, complete with a comment explaining that it is deliberate. And a ship that
     * stops declaring its own is glued to the first thing it touches again, with no
     * error, no log line and no failing test: the only symptom is a player saying the
     * car will not move.
     */
    [Fact]
    public void TheFrictionOfAPairComesFromItsBodies()
    {
        string root = _repoRoot();

        string callbacks = File.ReadAllText(
            Path.Combine(root, "JoyceCode/engine/physics/NarrowPhaseCallbacks.cs"));

        Assert.DoesNotContain("FrictionCoefficient = 1f", callbacks);
        Assert.Contains("CollisionProperties.CombineFriction", callbacks);

        string hover = File.ReadAllText(
            Path.Combine(root, "nogameCode/nogame/modules/playerhover/HoverModule.cs"));

        Assert.True(hover.Contains("Friction = FrictionShip"),
            "the player's ship no longer declares a friction coefficient of its own, so "
            + "it is back on the world default of "
            + $"{CollisionProperties.DefaultFriction} - which is a tyre, and the ship "
            + "has none. It will stick to the first surface it touches.");
    }
}
