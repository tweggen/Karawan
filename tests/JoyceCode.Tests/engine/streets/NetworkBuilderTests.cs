using System;
using System.Linq;
using System.Numerics;
using engine.streets;
using engine.streets.generation;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * Direct coverage for the one operation with the most invariants riding on it.
 *
 * Splitting a stored stroke has to keep two octrees, the adjacency set, the InStore
 * flags and the per-point angle arrays in sync. Until WP-1 this was open coded inside
 * Generator.Generate() and had no test at all.
 */
public class NetworkBuilderTests
{
    private const float ClusterSize = 1000f;


    private static StreetPoint _pointAt(float x, float y)
    {
        var sp = new StreetPoint() { ClusterId = 0 };
        sp.SetPos(x, y);
        return sp;
    }


    /**
     * A store holding one horizontal stroke from (0,0) to (100,0).
     */
    private static (StrokeStore Store, NetworkBuilder Builder, Stroke Stroke,
                    StreetPoint A, StreetPoint B) _oneStroke()
    {
        var clusterDesc = StreetHarness.MakeCluster("networkbuilder", ClusterSize);
        var store = new StrokeStore(ClusterSize);

        var a = _pointAt(0f, 0f);
        var b = _pointAt(0f, 0f);
        var stroke = Stroke.CreateByAngleFrom(clusterDesc, a, b, 0f, 100f, true, 1.0f);
        store.AddStroke(stroke);

        return (store, new NetworkBuilder(store), stroke, a, b);
    }


    [Fact]
    public void SplitProducesTwoHalvesSpanningTheOriginal()
    {
        var (store, builder, stroke, a, b) = _oneStroke();
        var at = _pointAt(50f, 0f);

        Stroke tail = builder.SplitStrokeAt(stroke, at);

        Assert.Same(a, stroke.A);
        Assert.Same(at, stroke.B);
        Assert.Same(at, tail.A);
        Assert.Same(b, tail.B);
    }


    [Fact]
    public void SplitLeavesEverythingInTheStore()
    {
        var (store, builder, stroke, a, b) = _oneStroke();
        var at = _pointAt(50f, 0f);

        Stroke tail = builder.SplitStrokeAt(stroke, at);

        Assert.Same(store, stroke.Store);
        Assert.Same(store, tail.Store);
        Assert.True(at.InStore, "the new junction must be in the store");
        Assert.True(a.InStore);
        Assert.True(b.InStore);

        Assert.Equal(2, store.GetStrokes().Count);
        Assert.Equal(3, store.GetStreetPoints().Count);
        Assert.Contains(at, store.GetStreetPoints());
    }


    /**
     * The adjacency set backs AreConnected, which the generator uses to reject
     * duplicate edges. A stale entry there would let a duplicate stroke through.
     */
    [Fact]
    public void SplitRewiresTheAdjacencySet()
    {
        var (store, builder, stroke, a, b) = _oneStroke();
        var at = _pointAt(50f, 0f);

        Assert.True(store.AreConnected(a, b), "precondition: A and B start out connected");

        builder.SplitStrokeAt(stroke, at);

        Assert.True(store.AreConnected(a, at));
        Assert.True(store.AreConnected(at, b));
        Assert.False(store.AreConnected(a, b),
            "A and B must no longer be directly connected after the split");
    }


    /**
     * The angle arrays drive the minimum-angle constraint. If the new junction did not
     * know about both halves, strokes could later be emitted on top of one another.
     */
    [Fact]
    public void SplitUpdatesTheAngleArrays()
    {
        var (store, builder, stroke, a, b) = _oneStroke();
        var at = _pointAt(50f, 0f);

        Stroke tail = builder.SplitStrokeAt(stroke, at);

        var atStrokes = at.GetAngleArray();
        Assert.Contains(stroke, atStrokes);
        Assert.Contains(tail, atStrokes);
        Assert.Equal(2, atStrokes.Count);

        Assert.Contains(stroke, a.GetAngleArray());
        Assert.Contains(tail, b.GetAngleArray());
    }


    /**
     * The stroke octree is what the intersection test queries. A split half that never
     * made it in would be invisible to every later candidate.
     */
    [Fact]
    public void SplitHalvesAreFindableThroughTheStrokeOctree()
    {
        var (store, builder, stroke, a, b) = _oneStroke();
        var at = _pointAt(50f, 0f);

        Stroke tail = builder.SplitStrokeAt(stroke, at);

        /*
         * A point just off the middle of each half must find that half as its
         * closest stroke.
         */
        var probeHead = _pointAt(25f, 5f);
        var probeTail = _pointAt(75f, 5f);

        Assert.Same(stroke, store.GetClosestStroke(probeHead, 20f)?.StrokeExists);
        Assert.Same(tail, store.GetClosestStroke(probeTail, 20f)?.StrokeExists);
    }


    [Fact]
    public void SplittingAStrokeFromAnotherStoreIsRefused()
    {
        var (_, _, stroke, _, _) = _oneStroke();
        var otherStore = new StrokeStore(ClusterSize);
        var otherBuilder = new NetworkBuilder(otherStore);

        Assert.Throws<InvalidOperationException>(
            () => otherBuilder.SplitStrokeAt(stroke, _pointAt(50f, 0f)));
    }


    [Fact]
    public void SplittingAtAnAlreadyStoredPointIsRefused()
    {
        var (store, builder, stroke, a, _) = _oneStroke();

        Assert.Throws<InvalidOperationException>(() => builder.SplitStrokeAt(stroke, a));
    }


    /**
     * AC-1.4. This guard already existed in Stroke._setA/_setB; it had simply never
     * been covered. It is the invariant that makes SplitStrokeAt's remove-first
     * ordering mandatory rather than stylistic.
     */
    [Fact]
    public void AStoredStrokeRefusesToExchangeItsEndpoints()
    {
        var (store, _, stroke, _, _) = _oneStroke();
        var other = _pointAt(10f, 10f);

        Assert.Throws<InvalidOperationException>(() => stroke.A = other);
        Assert.Throws<InvalidOperationException>(() => stroke.B = other);
    }
}
