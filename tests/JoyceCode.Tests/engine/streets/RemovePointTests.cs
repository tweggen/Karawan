using System;
using System.Linq;
using engine.streets;
using engine.streets.generation;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * WP-B2.4: taking a junction out of a network.
 *
 * ⚠️ THE OCTREE IS THE WHOLE POINT, AND IT IS THE HALF THAT LOOKS OPTIONAL. StrokeStore
 * keeps its junctions twice - in _listPoints, which is what every consumer enumerates,
 * and in _octreeSP, which is what the generator's two proximity queries search. A removal
 * that drops the list entry only leaves a GHOST: a junction that no stroke touches, that
 * nothing renders and nothing walks, and that still wins FindClosestBelowButNot and
 * GetClosestPoint - so candidates snap onto it and the streets built from them end
 * nowhere. That is worse than not having a removal primitive at all, because it looks
 * correct.
 *
 * PolishStreetPoints has done exactly that since it was written, and gets away with it
 * only because it runs after Generate() has returned and nothing queries the point octree
 * afterwards. Lifting a corridor onto a deck does not get away with it, which is why the
 * primitive is written now rather than when the lift is.
 *
 * Every assertion below goes through the STORE'S OWN QUERIES rather than reflecting on
 * _octreeSP. A test that inspected the field would pass for a store whose queries read
 * something else, and it is the queries that decide where a street goes.
 */
public class RemovePointTests
{
    private const float ClusterSize = 2000f;


    private static StreetPoint _pointAt(float x, float y)
    {
        var sp = new StreetPoint() { ClusterId = 0 };
        sp.SetPos(x, y);
        return sp;
    }


    private static Stroke _commit(StrokeStore store, StreetPoint a, StreetPoint b)
    {
        var s = new Stroke()
        {
            ClusterId = 0, IsPrimary = true, Weight = 1f, Level = 0, Kind = StrokeKind.Street
        };
        s.A = a;
        s.B = b;
        store.AddStroke(s);

        return s;
    }


    /**
     * A probe junction beside the point under test, of the kind the generator uses when
     * it asks "is there anything here already".
     */
    private static StreetPoint _probeAt(float x, float y)
    {
        var sp = new StreetPoint() { ClusterId = 0 };
        sp.SetPos(x, y);

        return sp;
    }


    private static Stroke _probeStrokeThrough(float x0, float y0, float x1, float y1)
    {
        var s = new Stroke()
        {
            ClusterId = 0, IsPrimary = true, Weight = 1f, Level = 0, Kind = StrokeKind.Street
        };
        s.A = _pointAt(x0, y0);
        s.B = _pointAt(x1, y1);

        return s;
    }


    /**
     * THE POSITIVE CONTROL AND THE GATE IN ONE.
     *
     * Both proximity queries find the junction while it is in the network - without that
     * half, the assertions after the removal would pass for a store that never held it -
     * and neither finds it afterwards.
     */
    [Fact]
    public void ARemovedJunctionIsNoLongerFoundByEitherProximityQuery()
    {
        var store = new StrokeStore(ClusterSize);

        var doomed = _pointAt(0f, 0f);
        var keep = _pointAt(300f, 0f);
        var stroke = _commit(store, doomed, keep);

        var probePoint = _probeAt(10f, 0f);
        var probeStroke = _probeStrokeThrough(-100f, 20f, 100f, 20f);

        Assert.Same(doomed, store.FindClosestBelowButNot(probePoint, 50f, null));
        Assert.Same(doomed, store.GetClosestPoint(probeStroke, 50f)?.StreetPoint);
        Assert.Contains(doomed, store.GetStreetPoints());

        /*
         * Strokes first: a junction that still carries one may not be removed, and this
         * is how a caller is meant to get there.
         */
        store.Remove(stroke);
        store.RemovePoint(doomed);

        Assert.DoesNotContain(doomed, store.GetStreetPoints());
        Assert.False(doomed.InStore);

        Assert.Null(store.FindClosestBelowButNot(probePoint, 50f, null));
        Assert.Null(store.GetClosestPoint(probeStroke, 50f)?.StreetPoint);
    }


    /**
     * And the junction that stayed is still found, so the removal did not simply empty
     * the index.
     */
    [Fact]
    public void TheJunctionsThatStayAreStillFound()
    {
        var store = new StrokeStore(ClusterSize);

        var doomed = _pointAt(0f, 0f);
        var keep = _pointAt(300f, 0f);
        var other = _pointAt(600f, 0f);
        var doomedStroke = _commit(store, doomed, keep);
        _commit(store, keep, other);

        store.Remove(doomedStroke);
        store.RemovePoint(doomed);

        Assert.Same(keep, store.FindClosestBelowButNot(_probeAt(290f, 0f), 50f, null));
        Assert.Same(other,
            store.GetClosestPoint(_probeStrokeThrough(560f, 20f, 640f, 20f), 50f)?.StreetPoint);
    }


    /**
     * Removing a junction that still carries strokes would leave those strokes' endpoints
     * naming a junction the network does not have - a broken graph rather than a smaller
     * one. Refused, and refused before anything is taken out.
     */
    [Fact]
    public void AJunctionThatStillCarriesStrokesIsRefused()
    {
        var store = new StrokeStore(ClusterSize);

        var a = _pointAt(0f, 0f);
        var b = _pointAt(300f, 0f);
        _commit(store, a, b);

        Assert.Throws<InvalidOperationException>(() => store.RemovePoint(a));

        Assert.Contains(a, store.GetStreetPoints());
        Assert.Same(a, store.FindClosestBelowButNot(_probeAt(10f, 0f), 50f, null));
    }


    [Fact]
    public void AJunctionThatIsNotInTheStoreIsRefused()
    {
        var store = new StrokeStore(ClusterSize);
        _commit(store, _pointAt(0f, 0f), _pointAt(300f, 0f));

        Assert.Throws<InvalidOperationException>(() => store.RemovePoint(_pointAt(50f, 50f)));
    }


    /**
     * The one caller in the tree, driven: PolishStreetPoints drops strokeless junctions,
     * and they have to leave the octree with the list.
     *
     * A junction becomes strokeless when the only stroke touching it is removed, which is
     * how a lift will produce them.
     */
    [Fact]
    public void PolishStreetPointsTakesItsDeadJunctionsOutOfTheOctreeToo()
    {
        var store = new StrokeStore(ClusterSize);

        var doomed = _pointAt(0f, 0f);
        var keep = _pointAt(300f, 0f);
        var other = _pointAt(600f, 0f);
        var doomedStroke = _commit(store, doomed, keep);
        _commit(store, keep, other);

        store.Remove(doomedStroke);
        Assert.Same(doomed, store.FindClosestBelowButNot(_probeAt(10f, 0f), 50f, null));

        store.PolishStreetPoints();

        Assert.DoesNotContain(doomed, store.GetStreetPoints());
        Assert.Null(store.FindClosestBelowButNot(_probeAt(10f, 0f), 50f, null));
        Assert.Null(store.GetClosestPoint(_probeStrokeThrough(-100f, 20f, 100f, 20f), 50f));

        /*
         * And it removed only the strokeless one.
         */
        Assert.Equal(2, store.GetStreetPoints().Count);
    }


    /**
     * The ghost, demonstrated end to end rather than described: a candidate laid past the
     * place a removed junction used to be must not snap onto it.
     *
     * This is the failure the primitive exists to prevent, and it is driven through
     * Generate() so that it is the generator's own use of the octree under test and not a
     * paraphrase of it. Leave the octree entry behind and the grown network acquires a
     * junction at (0,0) that no removal ever took out.
     */
    [Fact]
    public void AGrowingStreetDoesNotSnapOntoARemovedJunction()
    {
        var store = new StrokeStore(ClusterSize);

        var doomed = _pointAt(0f, 0f);
        var keep = _pointAt(400f, 0f);
        var doomedStroke = _commit(store, doomed, keep);

        store.Remove(doomedStroke);
        store.RemovePoint(doomed);

        var cd = StreetHarness.MakeCluster("removepoint", ClusterSize);
        var g = new Generator();
        g.SetAnnotation("removepoint");
        g.Reset("streets-removepoint", store, cd);
        g.SetBounds(-500f, -500f, 500f, 500f);

        /*
         * Straight at where the junction used to be, ending 20 m short of it - well
         * inside SnapToNearbyPointConstraint's 30 m reach.
         */
        var a = _pointAt(-200f, 0f);
        g.AddStartingStroke(Stroke.CreateByAngleFrom(
            cd, a, new StreetPoint() { ClusterId = 0 }, 0f, 180f, true, 1.1f));

        g.Generate();

        Assert.DoesNotContain(store.GetStreetPoints(), sp => sp == doomed);
        Assert.DoesNotContain(store.GetStrokes(), s => s.A == doomed || s.B == doomed);
    }
}
