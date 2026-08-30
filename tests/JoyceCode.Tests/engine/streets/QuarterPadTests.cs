using System;
using System.Collections.Generic;
using System.Numerics;
using engine.streets;
using engine.world;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * A city block is a pad: one plane, tilted onto its own corners.
 *
 * Everything a quarter carries asks it for the ground - the floor mesh, the buildings,
 * the trees, the shop fronts, the doors NPCs walk to - so what matters is not only that
 * the answer is sensible but that it is the SAME answer for all of them. A plane is what
 * buys that: any caller can evaluate it anywhere, exactly, with no reference to the mesh.
 */
public class QuarterPadTests
{
    private const float ClusterSize = 2000f;


    /**
     * A ring of delimiters round a block, wired the way QuarterGenerator wires them:
     * delimiter i is the corner at junction i, and the edge leaving it along the stroke
     * from junction i to junction i+1.
     *
     * A distinct StreetPoint per corner rather than one shared junction, so that a pad
     * that read the NEXT delimiter's junction - the shape the generator used to produce -
     * fits a rotated set of heights and this file can see it. The strokes are wired too,
     * even though the pad does not read them, so the fixture is a whole delimiter and not
     * the half this file happens to use.
     */
    internal static void AddRing(Quarter quarter, IReadOnlyList<Vector2> corners)
    {
        int n = corners.Count;
        var points = new StreetPoint[n];
        for (int i = 0; i < n; ++i)
        {
            points[i] = new StreetPoint() { ClusterId = 0 };
            points[i].SetPos(corners[i].X, corners[i].Y);
        }

        for (int i = 0; i < n; ++i)
        {
            int next = (i + 1) % n;
            var delim = new QuarterDelim();
            delim.SetEdge(
                corners[i], points[i],
                new Stroke { ClusterId = 0, A = points[i], B = points[next] });
            quarter.AddQuarterDelim(delim);
        }
    }


    /**
     * A square block whose four corners are junctions of a real store, so the pad is
     * fitted to heights the height source actually produced.
     *
     * @param fHeight
     *     Ground height from a junction's plan position, or null for a flat city.
     */
    private static Quarter _block(Func<float, float, float> fHeight, float side = 100f)
    {
        var cd = StreetHarness.MakeCluster("quarterpad", ClusterSize);
        cd.AverageHeight = 20f;
        cd.StreetHeightSource = null == fHeight
            ? new FlatStreetHeight(cd)
            : new FuncStreetHeight(fHeight);

        var quarter = new Quarter { ClusterDesc = cd };

        AddRing(quarter, new[]
        {
            new Vector2(0f, 0f),
            new Vector2(side, 0f),
            new Vector2(side, side),
            new Vector2(0f, side),
        });

        return quarter;
    }


    /**
     * The corner of a block takes the height of the junction it stands ON, which is its
     * own delimiter's StreetPoint.
     *
     * The pad is fitted to (corner position, corner height) pairs, so pairing a corner
     * with any neighbouring junction shifts every corner's height by a whole street's
     * worth of slope while leaving the plan geometry, the mesh and the routing exactly
     * right.
     */
    [Fact]
    public void ThePadTakesEachCornersOwnJunction()
    {
        var cd = StreetHarness.MakeCluster("quarterpad-pairing", ClusterSize);
        cd.AverageHeight = 20f;
        cd.StreetHeightSource = new FuncStreetHeight((x, z) => 100f + 0.2f * x);

        var quarter = new Quarter { ClusterDesc = cd };
        AddRing(quarter, new[]
        {
            new Vector2(0f, 0f),
            new Vector2(100f, 0f),
            new Vector2(100f, 100f),
            new Vector2(0f, 100f),
        });

        /*
         * The height field varies only in X and the block is a square, so the pad
         * reproduces it exactly - if and only if each corner took its own junction.
         * Reading a neighbouring delimiter's StreetPoint instead rotates the four heights
         * by one position round the block, which is 20 m of shift on this slope.
         */
        foreach (var d in quarter.GetDelims())
        {
            Assert.Equal(
                100f + 0.2f * d.StartPoint.X,
                quarter.CornerGroundHeightAt(d), 3);
            Assert.Equal(
                quarter.CornerGroundHeightAt(d),
                quarter.GroundHeightAt(d.StartPoint), 3);
        }
    }


    /**
     * A flat city answers with its average, exactly - not to within a rounding error.
     *
     * This is the property the whole line of work is gated on, and a least squares plane
     * through four equal heights does NOT reproduce its input bit for bit. Hence the
     * short circuit, and hence this test.
     */
    [Fact]
    public void AFlatCityAnswersWithItsAverageExactly()
    {
        /*
         * Ten corners and a height with no exact binary form, on purpose - and the
         * count matters. A tidy fixture cannot show this: with four, or even seven,
         * corners the sum-then-divide happens to round-trip and a version with no short
         * circuit passes. From ten it does not, and 20.1 comes back as 20.1000022.
         */
        var cd = StreetHarness.MakeCluster("quarterpad-flat", ClusterSize);
        cd.AverageHeight = 20.1f;
        cd.StreetHeightSource = new FlatStreetHeight(cd);

        var quarter = new Quarter { ClusterDesc = cd };
        var corners = new List<Vector2>();
        for (int i = 0; i < 10; ++i)
        {
            float a = i * 0.897f;
            corners.Add(new Vector2(
                37.3f * MathF.Cos(a) + 111.7f, 41.9f * MathF.Sin(a) - 73.1f));
        }

        AddRing(quarter, corners);

        Assert.Equal(20.1f, quarter.GroundHeightAt(new Vector2(111.7f, -73.1f)));
        Assert.Equal(20.1f, quarter.GroundHeightAt(new Vector2(0f, 0f)));
        Assert.Equal(20.1f, quarter.GroundHeightAt(new Vector2(137.5f, 61.25f)));
    }


    /**
     * Corners on a plane put the pad on that plane, so a block on an evenly sloping
     * hillside meets the streets around it with no residual at all.
     */
    [Fact]
    public void APadOnAPlaneReproducesItExactly()
    {
        var quarter = _block((x, z) => 50f + 0.06f * x - 0.02f * z);

        Assert.Equal(50f, quarter.GroundHeightAt(new Vector2(0f, 0f)), 3);
        Assert.Equal(56f, quarter.GroundHeightAt(new Vector2(100f, 0f)), 3);
        Assert.Equal(54f, quarter.GroundHeightAt(new Vector2(100f, 100f)), 3);

        /*
         * And in the middle, where no corner is - which is where the buildings go.
         */
        Assert.Equal(52f, quarter.GroundHeightAt(new Vector2(50f, 50f)), 3);
    }


    /**
     * Corners that are not coplanar get the best fit rather than a wild tilt, and the
     * pad stays inside the range of its own corners.
     *
     * A block cannot be made to touch four non-coplanar corners; what it must not do is
     * leave the ground entirely trying.
     */
    [Fact]
    public void APadStaysWithinTheRangeOfItsCorners()
    {
        var quarter = _block((x, z) => 40f + 0.05f * x + 0.03f * z + 4f * MathF.Sin(x * 0.11f));

        float lo = Single.PositiveInfinity, hi = Single.NegativeInfinity;
        foreach (var d in quarter.GetDelims())
        {
            float h = 40f + 0.05f * d.StartPoint.X + 0.03f * d.StartPoint.Y
                      + 4f * MathF.Sin(d.StartPoint.X * 0.11f);
            lo = Single.Min(lo, h);
            hi = Single.Max(hi, h);
        }

        for (float x = 0f; x <= 100f; x += 10f)
        {
            for (float z = 0f; z <= 100f; z += 10f)
            {
                float h = quarter.GroundHeightAt(new Vector2(x, z));
                Assert.InRange(h, lo - 0.01f, hi + 0.01f);
            }
        }
    }


    /**
     * The pad is a plane, so it is linear - which is what lets a caller with a position
     * and a caller with a mesh vertex agree without either knowing about the other.
     */
    [Fact]
    public void ThePadIsLinear()
    {
        var quarter = _block((x, z) => 30f + 0.08f * x - 0.04f * z);

        float a = quarter.GroundHeightAt(new Vector2(10f, 20f));
        float b = quarter.GroundHeightAt(new Vector2(90f, 20f));
        float mid = quarter.GroundHeightAt(new Vector2(50f, 20f));

        Assert.Equal((a + b) / 2f, mid, 3);
    }


    /**
     * Corners in a line in plan cannot determine a tilt, so the pad falls back to their
     * mean rather than solving a singular system and flying off.
     */
    [Fact]
    public void CollinearCornersFallBackToTheMean()
    {
        var cd = StreetHarness.MakeCluster("quarterpad-degenerate", ClusterSize);
        cd.AverageHeight = 20f;
        cd.StreetHeightSource = new FuncStreetHeight((x, z) => 10f + 0.1f * x);

        var quarter = new Quarter { ClusterDesc = cd };
        AddRing(quarter, new[] { new Vector2(0f, 0f), new Vector2(50f, 0f), new Vector2(100f, 0f) });

        /*
         * Heights are 10, 15 and 20, so the mean is 15 - and it must be that everywhere,
         * since nothing determines which way the block should lean.
         */
        Assert.Equal(15f, quarter.GroundHeightAt(new Vector2(0f, 0f)), 3);
        Assert.Equal(15f, quarter.GroundHeightAt(new Vector2(100f, 400f)), 3);
    }


    /**
     * An estate knows the block it was added to, which is how anything standing on an
     * estate - a polytope, a tree - finds the pad under it.
     */
    [Fact]
    public void AnEstateKnowsItsBlock()
    {
        var quarter = _block((x, z) => 12f + 0.03f * x);
        var estate = new Estate { ClusterDesc = quarter.ClusterDesc };

        Assert.Null(estate.Quarter);
        quarter.AddEstate(estate);
        Assert.Same(quarter, estate.Quarter);
    }
}
