using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using engine.joyce;
using engine.streets;
using engine.streets.generation;
using engine.world;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * Whether the drawn road is the surface it says it is.
 *
 * §7o gave every carriageway a RoadSurface: each SIDE climbs between its own two section
 * points, so the road meets the flat junction caps at its corners and the block kerbs along
 * their whole length. §7r then showed that the surface reproduces every emitted vertex to
 * 8e-6 m - and that the mesh BETWEEN those vertices does not follow it, because a
 * carriageway's rows were emitted one texture length apart and a texture length is four
 * street widths, up to 88 m.
 *
 * The reason is not row length on its own, and that was measured before anything was
 * changed. A carriageway is a RULED surface between two straight kerb chords of different
 * slope, i.e. a hyperbolic paraboloid, and a quad cut from one departs from it by a quarter
 * of the twist it spans, at the midpoint of the diagonal its two triangles share. So the
 * error is proportional to the row's length TIMES the difference between the two sides'
 * slopes, and it is exactly zero where those are equal - every stroke of a flat city, and
 * every straight one in any city.
 *
 * ⚠️ **The hypothesis this work started from - that the error is concentrated where a row
 * STRADDLES a kink in the piecewise profile - is false, and refuted by one count: over the
 * five cities, ZERO of 4 608 row spans contain a section point strictly inside them.** They
 * cannot: the rows run from the further of the two A section points to the nearer of the
 * two B ones, so every row span lies inside both sides' climbing windows by construction.
 * What straddles a kink is the END WEDGE, the single triangle between a junction's seam and
 * the first full-width row, and that had the worse distribution of the two.
 */
public class RoadTessellationTests
{
    /**
     * How close a road triangle must stay to the surface it is cut from.
     *
     * RoadSurface.MaxSag is what the emission aims at; the margin is for the sampling here
     * being a barycentric grid rather than an exact maximisation, and for the last row of a
     * span being the remainder rather than a full step.
     */
    private const float SagTolerance = RoadSurface.MaxSag * 1.05f;


    public static IEnumerable<object[]> Cities()
    {
        yield return new object[] { "seed000", 500f };

        /*
         * The seed whose junction footprints overlap on some strokes, so that there is no
         * carriageway between them at all and _generateStreetRun emits a filler quad
         * instead of any rows. Carried for the same reason KerbSeamTests and
         * RouteRibbonRoadTests carry it: no other seed reaches that branch, so a rule about
         * what happens there cannot be broken by any amount of data from the others.
         */
        yield return new object[] { "seed008", 500f };
        yield return new object[] { "Yelukhdidru", 800f };
        yield return new object[] { "seed000", 1500f };
        yield return new object[] { "Yelukhdidru", 3000f };
    }


    private sealed class City
    {
        internal ClusterDesc Cluster;
        internal StrokeStore Strokes;
        internal Dictionary<Stroke, Mesh> ByStroke;
    }


    private static City _city(string idString, float size, bool flat)
    {
        var cd = StreetHarness.MakeCluster(idString, size);
        cd.AverageHeight = 20f;

        var strokes = StreetHarness.Generate(idString, size);
        cd.StreetHeightSource = flat
            ? new FlatStreetHeight(cd)
            : ShippedTerrain.StreetHeightsOf(cd, strokes);

        return new City
        {
            Cluster = cd,
            Strokes = strokes,
            ByStroke = StreetGeometryHarness.GenerateEachStroke(cd, strokes)
        };
    }


    /**
     * The axial distances of a stroke's four section points, in the surface's own frame.
     */
    private static bool _corners(
        Stroke stroke, out float daMin, out float daMax, out float dbMin, out float dbMax)
    {
        daMin = daMax = dbMin = dbMax = 0f;
        if (!RoadSurface.TryCornersOf(stroke, out var al, out var ar, out var bl, out var br,
                out _)) return false;

        Vector2 o = stroke.A.Pos;
        Vector2 u = stroke.Unit;
        float dal = Vector2.Dot(al - o, u);
        float dar = Vector2.Dot(ar - o, u);
        float dbl = Vector2.Dot(bl - o, u);
        float dbr = Vector2.Dot(br - o, u);

        daMin = Single.Min(dal, dar);
        daMax = Single.Max(dal, dar);
        dbMin = Single.Min(dbl, dbr);
        dbMax = Single.Max(dbl, dbr);

        return true;
    }


    /**
     * Whether a stroke's section points are where its rows are, i.e. at exactly plus or
     * minus half its street width off its own centre line.
     *
     * §7o's own recorded defect: a handful are not, and on those the mesh's rows and the
     * mesh's kerbs are two different lines, so no surface can be on both. Excluded here by
     * name and counted, never averaged away - see
     * RouteRibbonRoadTests.TheCarriagewaySurfaceReproducesEveryVertexOfTheRoadItDescribes,
     * which bounds how many of them there may be.
     */
    private static bool _isSkew(Stroke stroke)
    {
        if (!RoadSurface.TryCornersOf(stroke, out var al, out var ar, out var bl, out var br,
                out _)) return true;

        float hsw = stroke.StreetWidth() / 2f;

        foreach (var (p, sign) in new[] { (al, -1f), (ar, 1f), (bl, -1f), (br, 1f) })
        {
            if (Single.Abs(Vector2.Dot(p - stroke.A.Pos, stroke.Normal) - sign * hsw) >= 1e-3f)
            {
                return true;
            }
        }

        return false;
    }


    /**
     * The worst departure of one triangle from the surface, over a barycentric grid.
     *
     * A grid rather than the analytic maximum because what is being asserted is a property
     * of the DRAWN triangle, and the drawn triangle is what a rasteriser interpolates: the
     * three vertices being on the surface is exactly the thing that is true and not enough.
     */
    private static float _worstOver(
        in RoadSurface surface, in Vector3 a, in Vector3 b, in Vector3 c)
    {
        const int N = 12;
        float worst = 0f;

        for (int x = 0; x <= N; ++x)
        for (int y = 0; y + x <= N; ++y)
        {
            float l1 = (float)x / N, l2 = (float)y / N, l3 = 1f - l1 - l2;
            Vector3 p = a * l1 + b * l2 + c * l3;
            worst = Single.Max(worst,
                Single.Abs(p.Y - surface.SurfaceHeightAt(new Vector2(p.X, p.Z))));
        }

        return worst;
    }


    /**
     * THE gate: the road a player drives on is the road the rest of the city is built
     * against, everywhere and not only at its vertices.
     *
     * Before, on the shipped terrain: per row quad median 0.039 m, p95 0.25 m, worst 1.00 m
     * over the five cities; per end wedge median 0.064 m, p95 0.36 m, worst 0.90 m.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void TheDrawnRoadStaysOnItsOwnSurface(string idString, float size)
    {
        var city = _city(idString, size, false);

        int nTriangles = 0, nSkipped = 0;
        float worst = 0f;
        string worstWhere = "";

        foreach (var stroke in city.Strokes.GetStrokes())
        {
            var so = RoadSurface.OfStroke(stroke, city.Cluster.StreetHeightSource, Vector2.Zero);
            Assert.True(so.HasValue, $"{idString}/{size}: stroke {stroke.Sid} has no surface");

            var mesh = city.ByStroke[stroke];
            if (0 == mesh.Indices.Count) continue;

            Assert.True(_corners(stroke, out _, out float daMax, out float dbMin, out _));

            /*
             * The overlapping-footprint branch has no carriageway at all - only a filler
             * quad joining the two caps - and §7r recorded that the two caps there give the
             * road two heights up to 1.25 m apart at one place. That is a different open
             * defect and this file does not pretend to bound it.
             */
            bool skip = daMax > dbMin || _isSkew(stroke);

            for (int i = 0; i + 2 < mesh.Indices.Count; i += 3)
            {
                if (skip) { ++nSkipped; continue; }

                float d = _worstOver(so.Value,
                    mesh.Vertices[(int)mesh.Indices[i]],
                    mesh.Vertices[(int)mesh.Indices[i + 1]],
                    mesh.Vertices[(int)mesh.Indices[i + 2]]);

                ++nTriangles;
                if (d > worst)
                {
                    worst = d;
                    worstWhere = $"stroke {stroke.Sid}, {stroke.StreetWidth():F1} m wide";
                }
            }
        }

        Assert.True(nTriangles > 100,
            $"{idString}/{size}: only {nTriangles} triangles were measurable");
        Assert.True(nSkipped * 4 < nTriangles,
            $"{idString}/{size}: {nSkipped} of {nSkipped + nTriangles} triangles were skipped");

        Assert.True(worst < SagTolerance,
            $"{idString}/{size}: the drawn road is {worst:F4} m off its own surface, at "
            + worstWhere);
    }


    /**
     * The end wedges are EXACT, and that is not the same statement as the one above.
     *
     * Between a junction's seam - the straight line joining its two section points, beyond
     * which there is no carriageway at all, only the flat cap - and the first row spanning
     * the full width, the carriageway is one triangle with three corners fixed by the
     * seams: two on the seam at the junction's own height, one on the kerb of whichever
     * side is already climbing. Three corners admit exactly one plane, so there is no
     * tessellation choice there and nothing to converge to. RoadSurface says so, and this
     * asserts it at four orders of magnitude below the bound above, which is what
     * distinguishes "the plane" from "a fine enough approximation".
     *
     * Before, the surface blended the two rails over the wedge instead: measured over the
     * five cities at up to 0.90 m, a worse distribution than the rows at every percentile.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void AnEndWedgeIsExactlyThePlaneThroughItsOwnThreeCorners(string idString, float size)
    {
        var city = _city(idString, size, false);

        int nWedges = 0;
        float worst = 0f;

        foreach (var stroke in city.Strokes.GetStrokes())
        {
            var so = RoadSurface.OfStroke(stroke, city.Cluster.StreetHeightSource, Vector2.Zero);
            if (!so.HasValue) continue;

            var mesh = city.ByStroke[stroke];
            if (0 == mesh.Indices.Count) continue;
            if (!_corners(stroke, out _, out float daMax, out float dbMin, out _)) continue;
            if (daMax > dbMin || _isSkew(stroke)) continue;

            /*
             * _generateStreetRun emits the two wedges first, one triangle each, and the
             * rows after them - so the first six vertices of a stroke's mesh are its two
             * wedges and nothing else.
             */
            if (mesh.Vertices.Count < 6) continue;

            for (int i = 0; i < 6; i += 3)
            {
                worst = Single.Max(worst, _worstOver(so.Value,
                    mesh.Vertices[i], mesh.Vertices[i + 1], mesh.Vertices[i + 2]));
                ++nWedges;
            }
        }

        Assert.True(nWedges > 40, $"{idString}/{size}: only {nWedges} wedges were measured");
        Assert.True(worst < 1e-3f,
            $"{idString}/{size}: an end wedge is {worst:F6} m off the plane through its own "
            + "three corners");
    }


    /**
     * A FLAT city's carriageway is cut into exactly the rows it always was: one per texture
     * length, and a remainder.
     *
     * The whole point of gating the subdivision on the two sides' slopes rather than on row
     * length is that a surface with no twist in it needs none - so a flat city emits the
     * same vertices at the same floats, which is what street-geometry.json's five recorded
     * cities assert as hashes and what this asserts as a shape.
     *
     * A ramp is the same case for the same reason and is covered by
     * ASlopingStrokeWithTwoEqualSidesIsNotSubdividedEither below: OverpassBuilder builds
     * every ramp straight, so its two sides span the same axial window.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void AFlatCitysCarriagewayIsStillOneRowPerTextureLength(string idString, float size)
    {
        var city = _city(idString, size, true);

        int nRows = 0, nExactRows = 0;

        foreach (var stroke in city.Strokes.GetStrokes())
        {
            var so = RoadSurface.OfStroke(stroke, city.Cluster.StreetHeightSource, Vector2.Zero);
            if (!so.HasValue) continue;

            Assert.True(so.Value.IsLevel,
                $"{idString}/{size}: stroke {stroke.Sid} of a FLAT city is not level");
            Assert.True(Single.IsPositiveInfinity(so.Value.MaxRowSpan),
                $"{idString}/{size}: stroke {stroke.Sid} of a FLAT city asks to be cut into "
                + $"rows of {so.Value.MaxRowSpan} m");

            var mesh = city.ByStroke[stroke];
            if (mesh.Vertices.Count <= 6) continue;

            float texlen = stroke.StreetWidth() * 4f;
            if (!_corners(stroke, out _, out float daMax, out float dbMin, out _)) continue;

            /*
             * Every row a flat city emits is at a whole texture length, or at one of the two
             * ends of the run - which is the rule as it was written before §7s and is what
             * makes the texture tile. An extra row is at none of the three.
             */
            foreach (float d in _rowDistances(stroke, mesh))
            {
                ++nRows;

                bool isEnd = Single.Abs(d - daMax) < 1e-2f || Single.Abs(d - dbMin) < 1e-2f;
                float whole = Single.Round(d / texlen) * texlen;

                Assert.True(isEnd || Single.Abs(d - whole) < 1e-2f,
                    $"{idString}/{size}: stroke {stroke.Sid} of a FLAT city has a row at "
                    + $"{d:F3} m, which is neither a whole texture length of {texlen:F3} m "
                    + $"nor either end of its run [{daMax:F3}, {dbMin:F3}]");

                /*
                 * ⚠️ NOT asserted as a float, deliberately, and the reason is worth keeping:
                 * the loop computes the last row of a span as nextD itself rather than as
                 * currD + (nextD - currD) * 1, and those differ by one unit in the last
                 * place. That difference cannot be recovered from here - d is read back out
                 * of the emitted VERTEX, which was built as origin + unit * d and then
                 * projected again, so the round trip loses more precision than the ulp being
                 * looked for. street-geometry.json cannot see it either, since it rounds
                 * coordinates to the millimetre. The expression is written the exact way for
                 * §7n's reason - "the same float, not the same place to within a rounding" -
                 * and the mutation that changes it is recorded as a surviving one in §7s
                 * rather than pretended to be caught.
                 */
                if (!isEnd) ++nExactRows;
            }
        }

        Assert.True(nRows > 20, $"{idString}/{size}: only {nRows} rows were measured");
        Assert.True(nExactRows > 5,
            $"{idString}/{size}: only {nExactRows} rows landed on a whole texture length");
    }


    /**
     * The axial distance of every row vertex a stroke emitted, in emission order.
     *
     * The first six vertices are the two end wedges; everything after them is rows, two
     * vertices each.
     */
    private static List<float> _rowDistances(Stroke stroke, Mesh mesh)
    {
        var into = new List<float>();

        for (int i = 6; i + 1 < mesh.Vertices.Count; i += 2)
        {
            var v = mesh.Vertices[i];
            into.Add(Vector2.Dot(new Vector2(v.X, v.Z) - stroke.A.Pos, stroke.Unit));
        }

        return into;
    }


    /**
     * A stroke whose two sides span the same axial window carries no twist however steep it
     * is, so it is not subdivided either - which is what leaves every ramp OverpassBuilder
     * builds unchanged float for float.
     *
     * A fixture rather than a city, because a generated city on the shipped terrain has
     * almost no perfectly straight junctions and "no real data breaks this rule" is not the
     * same as "this rule holds".
     */
    [Fact]
    public void ASlopingStrokeWithTwoEqualSidesIsNotSubdividedEither()
    {
        var straight = RoadSurface.Of(
            Vector2.Zero, Vector2.UnitX,
            new Vector2(10f, -5f), new Vector2(10f, 5f),
            new Vector2(90f, -5f), new Vector2(90f, 5f),
            20f, 40f);

        Assert.False(straight.IsLevel);
        Assert.True(Single.IsPositiveInfinity(straight.MaxRowSpan));
        Assert.True(Single.IsPositiveInfinity(straight.MaxSpanAcross(4f)));

        /*
         * ...and one where they do not, so that the infinity above is a property of the
         * surface and not of the method.
         */
        var bent = RoadSurface.Of(
            Vector2.Zero, Vector2.UnitX,
            new Vector2(10f, -5f), new Vector2(20f, 5f),
            new Vector2(90f, -5f), new Vector2(90f, 5f),
            20f, 40f);

        Assert.True(Single.IsFinite(bent.MaxRowSpan));

        /*
         * A quad only 4 m across a 10 m road carries 40 % of the twist, so it may span two
         * and a half times as far. Asserted as the ratio rather than as a number, since it
         * is the proportionality that is the claim.
         */
        Assert.Equal(bent.MaxRowSpan * 10f / 4f, bent.MaxSpanAcross(4f), 3);
        Assert.Equal(10f, bent.Width, 3);
    }


    /**
     * Each edge of a strip drawn on the road crosses a junction's seam somewhere else, and
     * the breakpoints say where for BOTH of them.
     *
     * A seam - the line joining a junction's two section points, beyond which there is no
     * carriageway - runs across the road at an angle whenever the two section points are at
     * different distances along the stroke, which is every bent junction. So it is not one
     * distance; it is a distance per line along the road, and the strip's two edges do not
     * share it.
     *
     * Asserted as EQUALITY with the exact crossing rather than as "the ribbon ends up close
     * enough", because a bound that both a right answer and a nearly-right one satisfy
     * cannot tell them apart - §7p. Mutation testing found exactly that: giving both edges
     * the same lateral fraction passed every measurement in the suite.
     */
    [Fact]
    public void EachEdgeOfAStripGetsItsOwnCrossingOfEachSeam()
    {
        /*
         * A carriageway 10 m wide whose A junction is bent - its two section points are 10 m
         * apart along the stroke - and whose B junction is bent the other way.
         */
        var surface = RoadSurface.Of(
            Vector2.Zero, Vector2.UnitX,
            new Vector2(10f, -5f), new Vector2(20f, 5f),
            new Vector2(90f, -5f), new Vector2(80f, 5f),
            20f, 50f);

        /*
         * Two lines along the road at lateral fractions 0.3 and 0.7 - i.e. a 4 m strip down
         * the middle of a 10 m carriageway, which is what the satnav guideline is.
         */
        Vector2 rail0 = new(50f, -2f);
        Vector2 rail1 = new(50f, 2f);

        Span<float> six = stackalloc float[RoadSurface.NBreakpoints];
        surface.BreakpointsBetween(rail0, rail1, six);

        /*
         * seamA(u) runs from the left section point at u = 0 to the right one at u = 1, and
         * likewise at B. With the rails at 0.3 and 0.7 of a 10 m span that is 13 and 17 at
         * A, and 87 and 83 at B.
         */
        Assert.Equal(13f, six[0], 3);
        Assert.Equal(17f, six[1], 3);
        Assert.Equal(20f, six[2], 3);
        Assert.Equal(80f, six[3], 3);
        Assert.Equal(87f, six[4], 3);
        Assert.Equal(83f, six[5], 3);

        /*
         * ...and the two edges' crossings are DIFFERENT, which is the whole point and is
         * what a single shared fraction would lose.
         */
        Assert.NotEqual(six[0], six[1], 3);
        Assert.NotEqual(six[4], six[5], 3);
    }


    /**
     * Where the two junction footprints OVERLAP there are no wedge planes, and the surface
     * is the plain blend of its two rails.
     *
     * There is no carriageway between two overlapping footprints - _generateStreetRun emits
     * a four-corner filler quad and returns - so the two wedges would cover the same ground
     * and neither of their planes describes it. §7r recorded that the two junction caps
     * there disagree by up to 1.25 m, which is a different open defect; what this asserts is
     * only that the wedge machinery keeps out of it.
     *
     * A fixture because `seed008` is the only seed known to produce such a stroke at all and
     * one seed is not a rule - and because a mutation forcing the wedges on everywhere
     * passed the whole suite including `seed008`.
     */
    [Fact]
    public void AnOverlappingCarriagewayGetsNoWedgePlanes()
    {
        /*
         * The B section points come BEFORE the A ones along the stroke, which is what
         * damax > dbmin means.
         */
        var overlapping = RoadSurface.Of(
            Vector2.Zero, Vector2.UnitX,
            new Vector2(30f, -5f), new Vector2(40f, 5f),
            new Vector2(10f, -5f), new Vector2(20f, 5f),
            20f, 50f);

        /*
         * Down the middle, where the blend and either wedge plane are three different
         * numbers. The blend is the mean of the two sides' own heights, each of which is its
         * own chord fraction clamped to its own span.
         */
        Vector2 p = new(25f, 0f);

        float left = Single.Lerp(20f, 50f, Single.Clamp((25f - 30f) / (10f - 30f), 0f, 1f));
        float right = Single.Lerp(20f, 50f, Single.Clamp((25f - 40f) / (20f - 40f), 0f, 1f));

        Assert.Equal(0.5f * (left + right), overlapping.SurfaceHeightAt(p), 3);
    }


    /**
     * An extra row does not move the texture across it.
     *
     * Rows land on multiples of a texture length precisely so that the street texture
     * tiles, and UVProjector.GetUV computes v from the position's own distance along the
     * stroke minus a whole-tile offset - so an extra row INSIDE a tile is harmless if and
     * only if it keeps that offset. Incrementing it per emitted row instead, which is what
     * the loop did when every row was a whole tile, restarts the texture at each extra row.
     *
     * Asserted as the one property that distinguishes the two: within a stroke, v advances
     * at one rate per metre along it, whatever the rows are cut at.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void TheTextureRunsAtOneRatePerMetreWhateverTheRowsAreCutAt(
        string idString, float size)
    {
        var city = _city(idString, size, false);

        int nChecked = 0;

        foreach (var stroke in city.Strokes.GetStrokes())
        {
            var mesh = city.ByStroke[stroke];
            if (mesh.Vertices.Count <= 8) continue;
            if (null == mesh.UVs) continue;

            var ds = _rowDistances(stroke, mesh);
            float? rate = null;

            for (int i = 1; i < ds.Count; ++i)
            {
                float dd = ds[i] - ds[i - 1];
                if (dd < 0.5f) continue;

                Vector2 uv0 = mesh.UVs[6 + 2 * (i - 1)];
                Vector2 uv1 = mesh.UVs[6 + 2 * i];

                /*
                 * A pair straddling a tile boundary restarts v by construction and is not
                 * what this is about; the tiling itself is asserted by the row spacing test
                 * above.
                 */
                if (uv1.Y <= uv0.Y) continue;

                float r = (uv1.Y - uv0.Y) / dd;
                if (rate.HasValue)
                {
                    Assert.True(Single.Abs(r - rate.Value) < 1e-4f * Single.Max(1f, rate.Value),
                        $"{idString}/{size}: stroke {stroke.Sid} runs its texture at "
                        + $"{rate.Value:F6} per metre over one row and {r:F6} over the next");
                    ++nChecked;
                }
                else
                {
                    rate = r;
                }
            }
        }

        Assert.True(nChecked > 50,
            $"{idString}/{size}: only {nChecked} row pairs shared a tile, so nothing was "
            + "compared");
    }


    /**
     * What it costs, in the currency §7j's report was paid in.
     *
     * Observed over the five cities: 638, 510, 1 776, 10 568 and 50 382 road vertices,
     * against 370, 350, 972, 4 978 and 26 010 - i.e. 22 to 29 per stroke against 12 to 17.
     * The worst FRAGMENT of the 3 km city goes from 748 vertices to 1 448.
     *
     * The bound is per stroke rather than per city so that it says something about the rule
     * rather than about these five seeds, and it is what would fail if MaxSag were tightened
     * without anyone looking at the price.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void TheRoadDoesNotPayMoreThanFortyVerticesAStrokeForThis(
        string idString, float size)
    {
        var city = _city(idString, size, false);

        int nVertices = 0, nStrokes = 0;

        foreach (var stroke in city.Strokes.GetStrokes())
        {
            var mesh = city.ByStroke[stroke];
            if (0 == mesh.Indices.Count) continue;

            nVertices += mesh.Vertices.Count;
            ++nStrokes;
        }

        Assert.True(nStrokes > 20, $"{idString}/{size}: {nStrokes} strokes is too few");
        Assert.True(nVertices < 40 * nStrokes,
            $"{idString}/{size}: {nVertices} road vertices for {nStrokes} strokes");
    }
}
