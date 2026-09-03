using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using engine.joyce;
using engine.streets;
using engine.streets.generation;
using engine.world;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * Where the kerb meets the carriageway.
 *
 * There is no sidewalk object: GenerateClusterQuartersOperator extrudes a block's outline up
 * by QuarterSidewalkOffset, so the top face is the pavement and the SIDES are the kerb -
 * which makes the block's outline the line along which two independently generated meshes
 * are supposed to meet, and until now nothing said they did.
 *
 * The player's report was "a small gap between the bevel of the sidewalk and the street".
 * Measured on the shipped terrain over the four baseline cities, sampled at nine points
 * along every block edge, the kerb's underside stood clear of the road or sank into it by
 * more than the 0.15 m kerb itself at 27-31 % of positions, by more than half a metre at
 * 11 %, worst 6.49 m. **In the flat city the same measurement was exactly 0.000 m at every
 * percentile**, so this was never a defect anybody could have seen before the
 * terrain-following city became the default.
 *
 * The measurement here is against the road mesh's OWN triangles, read barycentrically, over
 * real generated cities - not against a recomputed profile, which would only restate the
 * implementation.
 */
public class KerbSeamTests
{
    /**
     * How far a block corner may sit from the offset of its own stroke's centre line before
     * the kerb is not the road's edge at all and no height rule can make the two meet.
     *
     * A block corner is a section point of its junction, and the two section points bounding
     * one stroke at its two ends lie on the same offset of that stroke's centre line - so
     * the block edge between them is collinear with the carriageway's edge. Measured over
     * all 2936 boundary edges of the four baselines, the deviation is 0.0002 m at the median
     * and 0.02 m at the 99th percentile, which is the 0.1 m grid StreetPoint.SetPos
     * quantises junction positions onto - i.e. it is not a plan gap, and that was measured
     * before any height was looked at.
     *
     * The exceptions are a separate, PLAN defect and are counted rather than tolerated:
     * StreetPoint._computeSectionArrayNoLock falls back to an averaged offset when two arms
     * are so nearly collinear that their offset lines meet more than 63 m out, and such a
     * corner lands up to 62 m off the line. 11 of 2477 edges in Yelukhdidru/3000 and none at
     * all in the other three. Nothing between 0.25 m and 1 m anywhere, so this separates the
     * two populations rather than cutting through one.
     */
    private const float PlanTolerance = 0.25f;

    /**
     * The seam is exact by construction, so this is single-precision noise on cluster
     * coordinates of up to 1500 m, not a fitted bound. Observed worst over all five cities
     * on all four grounds: 3 mm, at one of 22000 positions on a city 3 km across.
     */
    private const float SeamTolerance = 0.01f;


    public static IEnumerable<object[]> Cities()
    {
        yield return new object[] { "seed000", 500f };

        /*
         * The one seed known to reach _generateStreetRun's "a and b ends overlapping"
         * branch, where the two junction footprints meet and there is no carriageway
         * between them at all. That branch used to skip the shear entirely, leaving its
         * little filler quad flat at the A end's height while both kerbs beside it climbed;
         * none of the four baselines below contains one, so without this seed calling the
         * shear there could be deleted again and every test would still pass. Found the
         * same way StreetGeometryTests found it - by instrumenting the branch and scanning.
         */
        yield return new object[] { "seed008", 500f };
        yield return new object[] { "Yelukhdidru", 800f };
        yield return new object[] { "seed000", 1500f };
        yield return new object[] { "Yelukhdidru", 3000f };
    }


    /**
     * The grounds a city is measured on: the default flat one, two analytic slopes, and the
     * terrain the game actually ships.
     */
    private static IEnumerable<(string Name, Func<ClusterDesc, StrokeStore, IStreetHeightSource> Of)> _grounds()
    {
        yield return ("a flat city", (cd, _) => new FlatStreetHeight(cd));
        yield return ("a 5.8 % plane", (_, _) => new FuncStreetHeight((x, z) => 20f + 0.058f * x));
        yield return ("rolling ground", (_, _) => new FuncStreetHeight(
            (x, z) => 20f + 25f * Single.Sin(x / 220f) + 20f * Single.Cos(z / 190f)));
        yield return ("the shipped terrain", ShippedTerrain.StreetHeightsOf);
    }


    private sealed class City
    {
        internal ClusterDesc Cluster;
        internal StrokeStore Strokes;
        internal QuarterStore Quarters;

        /**
         * Each stroke's carriageway on its own. Near two nearly collinear arms the section
         * array's fallback lets two strokes' polygons overlap by up to 45 m, and a combined
         * mesh then has two answers for "how high is the road here" that differ by 1.6 m -
         * so a block's kerb has to be measured against the carriageway it actually runs
         * along, not against whatever else reaches across it.
         */
        internal Dictionary<Stroke, Mesh> ByStroke;

        private readonly Dictionary<Stroke, TriangleField> _fields = new();

        internal TriangleField SurfaceOf(Stroke stroke)
        {
            if (_fields.TryGetValue(stroke, out var f)) return f;

            f = TriangleField.Of(ByStroke[stroke]);
            _fields[stroke] = f;
            return f;
        }
    }


    /**
     * A whole generated city with its real road surfaces, through the shipping path.
     */
    private static City _city(
        string idString, float size,
        Func<ClusterDesc, StrokeStore, IStreetHeightSource> fGround)
    {
        var cd = StreetHarness.MakeCluster(idString, size);
        cd.AverageHeight = 20f;

        var strokes = StreetHarness.Generate(idString, size);
        cd.StreetHeightSource = fGround(cd, strokes);

        return new City
        {
            Cluster = cd,
            Strokes = strokes,
            Quarters = StreetHarness.GenerateQuarters(cd, strokes, idString),
            ByStroke = StreetGeometryHarness.GenerateEachStroke(cd, strokes)
        };
    }


    /**
     * How far a block edge's two corners are from the offset line of the stroke it runs
     * along - i.e. whether the kerb line and the carriageway's edge are the same line at
     * all, which is a question about PLAN and has nothing to do with height.
     */
    private static float _planOffsetOf(QuarterDelim delim, in Vector2 corner, in Vector2 next)
    {
        Stroke stroke = delim.Stroke;
        if (null == stroke) return Single.MaxValue;

        return Single.Max(_planOffsetOf(stroke, corner), _planOffsetOf(stroke, next));
    }


    private static float _planOffsetOf(Stroke stroke, in Vector2 p)
    {
        return Single.Abs(
            Single.Abs(Vector2.Dot(p - stroke.A.Pos, stroke.Normal))
            - stroke.StreetWidth() / 2f);
    }


    /**
     * THE gate: along every block edge, the bottom of the kerb is on the road.
     *
     * The kerb's underside is the block floor's outline, which the extrusion interpolates
     * linearly between two corners - so the sample is the outline's own linear
     * interpolation. The road is read off its emitted triangles at the same plan position.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void TheKerbRestsOnTheCarriagewayAlongEveryBlockEdge(string idString, float size)
    {
        foreach (var (gname, fGround) in _grounds())
        {
            var city = _city(idString, size, fGround);

            int nSamples = 0, nOffLine = 0, nUncovered = 0;
            float worst = 0f;
            string worstWhere = "";

            foreach (var q in city.Quarters.GetQuarters())
            {
                var outline = GenerateClusterQuartersOperator.FloorOutlineOf(q, 0f, 0f);
                var delims = q.GetDelims();
                int n = outline.Count;
                if (n < 3) continue;

                for (int i = 0; i < n; ++i)
                {
                    Vector3 v0 = outline[i], v1 = outline[(i + 1) % n];
                    Vector2 p0 = new(v0.X, v0.Z), p1 = new(v1.X, v1.Z);
                    Vector2 along = p1 - p0;
                    if (along.Length() < 2f) continue;

                    if (null == delims[i].Stroke) continue;
                    if (_planOffsetOf(delims[i], p0, p1) > PlanTolerance)
                    {
                        ++nOffLine;
                        continue;
                    }

                    var carriageway = city.SurfaceOf(delims[i].Stroke);

                    for (int k = 1; k < 10; ++k)
                    {
                        float t = k / 10f;
                        Vector2 p = p0 + along * t;

                        float? road = carriageway.HeightAt(p);
                        if (!road.HasValue)
                        {
                            ++nUncovered;
                            continue;
                        }

                        ++nSamples;
                        float err = Single.Abs((v0.Y + t * (v1.Y - v0.Y)) - road.Value);
                        if (err > worst)
                        {
                            worst = err;
                            worstWhere = $"the block at {q.GetCenterPoint()}, {t:F1} along an "
                                         + $"edge at {p}";
                        }
                    }
                }
            }

            Assert.True(nSamples > 100,
                $"{idString}/{size} on {gname}: only {nSamples} positions were measurable, "
                + "which proves too little");

            Assert.True(worst < SeamTolerance,
                $"{idString}/{size} on {gname}: the kerb is {worst:F3} m off the carriageway "
                + $"at {worstWhere}");

            /*
             * The two populations this test deliberately does not fold together, reported so
             * that a change which quietly grows either is visible rather than silently
             * absorbed into the exemption above.
             */
            Assert.True(nOffLine <= 12,
                $"{idString}/{size} on {gname}: {nOffLine} block edges are not on their own "
                + "stroke's offset line, up from the 11 the section array's near-collinear "
                + "fallback produces - that is a PLAN defect and this gate cannot see it");

            Assert.True(nUncovered * 200 < nSamples,
                $"{idString}/{size} on {gname}: the road mesh does not reach the kerb at "
                + $"{nUncovered} of {nSamples + nUncovered} positions");
        }
    }


    /**
     * ...and it did NOT rest on it before, on the same edges, by a lot.
     *
     * Without this the gate above is satisfied by anything at all that happens to agree
     * where it is sampled - a road collapsed to one height would pass it on a flat city and
     * nothing would say the flat city is the easy case. So the profile the carriageway used
     * to be sheared onto is reconstructed here and measured at the same positions: one
     * window along the stroke's CENTRE line, flat at the A end's height up to the further of
     * the two section points there, climbing, then flat at the B end's height.
     *
     * It agrees with the kerb chord at both ends and nowhere in between, which is exactly
     * why measuring at corners would have shown nothing.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void TheSingleWindowShearDidNotMeetTheKerb(string idString, float size)
    {
        var city = _city(idString, size, ShippedTerrain.StreetHeightsOf);

        var errors = new List<float>();

        foreach (var q in city.Quarters.GetQuarters())
        {
            var outline = GenerateClusterQuartersOperator.FloorOutlineOf(q, 0f, 0f);
            var delims = q.GetDelims();
            int n = outline.Count;
            if (n < 3) continue;

            for (int i = 0; i < n; ++i)
            {
                Stroke stroke = delims[i].Stroke;
                if (null == stroke) continue;

                Vector3 v0 = outline[i], v1 = outline[(i + 1) % n];
                Vector2 p0 = new(v0.X, v0.Z), p1 = new(v1.X, v1.Z);
                if ((p1 - p0).Length() < 2f) continue;
                if (_planOffsetOf(delims[i], p0, p1) > PlanTolerance) continue;

                if (!_oldWindowOf(stroke, city.Cluster.StreetHeightSource,
                        out float hA, out float hB, out float dFlatA, out float dFlatB))
                {
                    continue;
                }

                Vector2 origin = stroke.A.Pos;
                Vector2 unit = stroke.Unit;

                for (int k = 1; k < 10; ++k)
                {
                    float t = k / 10f;
                    Vector2 p = p0 + (p1 - p0) * t;
                    float d = Vector2.Dot(p - origin, unit);

                    float span = dFlatB - dFlatA;
                    float u = span > 0.001f
                        ? Single.Clamp((d - dFlatA) / span, 0f, 1f)
                        : (d <= dFlatA ? 0f : 1f);

                    errors.Add(Single.Abs((v0.Y + t * (v1.Y - v0.Y)) - (hA + u * (hB - hA))));
                }
            }
        }

        Assert.True(errors.Count > 100, $"only {errors.Count} positions measured");

        errors.Sort();
        float p95 = errors[(int)(0.95f * (errors.Count - 1))];
        float over = errors.Count(e => e > MetaGen.QuarterSidewalkOffset) / (float)errors.Count;

        Assert.True(p95 > 4f * SeamTolerance && over > 0.02f,
            $"{idString}/{size}: the single-window shear was only {p95:F3} m off the kerb at "
            + $"the 95th percentile and exceeded a kerb height at {over:P1} of positions, so "
            + "this baseline no longer describes the defect the per-side shear removed and "
            + "the gate above proves nothing");
    }


    /**
     * The window the carriageway used to be sheared onto, reconstructed from the same
     * section arrays GenerateClusterStreetsOperator reads.
     *
     * @returns false for a stroke whose two junction footprints overlap, which used to skip
     *     the shear altogether and is not a window at all.
     */
    private static bool _oldWindowOf(
        Stroke stroke, IStreetHeightSource heights,
        out float hA, out float hB, out float dFlatA, out float dFlatB)
    {
        hA = RoadSurface.HeightAtJunction(heights, stroke.A);
        hB = RoadSurface.HeightAtJunction(heights, stroke.B);
        dFlatA = 0f;
        dFlatB = stroke.Length;

        Vector2 origin = stroke.A.Pos;
        Vector2 unit = stroke.Unit;
        float hsw = stroke.StreetWidth() / 2f;

        if (!_cornersOf(stroke, stroke.A, out Vector2 al, out Vector2 ar)) return false;
        if (!_cornersOf(stroke, stroke.B, out Vector2 bl, out Vector2 br)) return false;

        float dal = Vector2.Dot(al - origin, unit), dar = Vector2.Dot(ar - origin, unit);
        float dbl = Vector2.Dot(bl - origin, unit), dbr = Vector2.Dot(br - origin, unit);

        dFlatA = Single.Max(dal, dar);
        dFlatB = Single.Min(dbl, dbr);

        return dFlatA <= dFlatB;
    }


    /**
     * The two section points bounding one stroke at one of its junctions, in the same order
     * the surface emission reads them.
     */
    private static bool _cornersOf(Stroke stroke, StreetPoint sp, out Vector2 left, out Vector2 right)
    {
        var angles = sp.GetAngleArray();
        var sections = sp.GetSectionArray();
        left = right = Vector2.Zero;

        if (angles.Count < 2 || sections.Count != angles.Count) return false;

        int idx = angles.IndexOf(stroke);
        if (idx < 0) return false;

        bool isA = stroke.A == sp;
        right = isA ? sections[idx] : sections[(idx + 1) % angles.Count];
        left = isA ? sections[(idx + 1) % angles.Count] : sections[idx];

        return true;
    }


    /**
     * The road still meets its junction caps: where a carriageway's corner stands on a cap's
     * corner, both are at that junction's one height.
     *
     * This is the property the single window existed to protect, and it has to survive the
     * change that replaced it. Heighting every vertex by its axial projection over the whole
     * stroke - the shape this all started from - tears the road open here by up to 1.8 m at
     * a bend while leaving the kerb seam untouched, so the two tests fail on different
     * mutations and neither implies the other.
     *
     * Asserted on the carriageway's own VERTEX at that corner rather than by reading its
     * surface there. A section point is not always exactly half a street width from the
     * centre line - one of the 9878 corners of Yelukhdidru/3000 is 0.19 m inside it - while
     * the carriageway's rows are emitted at exactly that width, so at such a corner the
     * surface overhangs its own boundary by a couple of decimetres and answers about its
     * interior. The corner itself is unambiguous.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void TheCarriagewayMeetsEveryJunctionCapAtItsCorners(string idString, float size)
    {
        var city = _city(idString, size, ShippedTerrain.StreetHeightsOf);

        int nChecked = 0, nMissing = 0;
        float worst = 0f;

        foreach (var stroke in city.Strokes.GetStrokes())
        {
            var mesh = city.ByStroke[stroke];

            foreach (var sp in new[] { stroke.A, stroke.B })
            {
                if (!_cornersOf(stroke, sp, out Vector2 left, out Vector2 right)) continue;

                float capHeight = RoadSurface.HeightAtJunction(
                    city.Cluster.StreetHeightSource, sp);

                foreach (var corner in new[] { left, right })
                {
                    bool found = false;
                    foreach (var v in mesh.Vertices)
                    {
                        if ((new Vector2(v.X, v.Z) - corner).Length() > 1e-3f) continue;

                        found = true;
                        worst = Single.Max(worst, Single.Abs(v.Y - capHeight));
                        ++nChecked;
                    }

                    if (!found) ++nMissing;
                }
            }
        }

        Assert.True(nChecked > 20, $"only {nChecked} cap corners were found in a road");
        Assert.Equal(0, nMissing);

        Assert.True(worst < SeamTolerance,
            $"{idString}/{size}: a carriageway stands {worst:F3} m off the junction cap it "
            + "runs into");
    }


    /**
     * A flat city's road surface is exactly one height, still.
     *
     * The whole city sits at AverageHeight + CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE and the
     * shear returns before touching a vertex, so this is an equality rather than a
     * tolerance. Stated here beside the seam because the flat path is the one this work no
     * longer defaults to and is therefore the one that will stop being exercised by hand.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void AFlatCitysRoadIsUntouched(string idString, float size)
    {
        var city = _city(idString, size, (cd, _) => new FlatStreetHeight(cd));
        float expected = city.Cluster.AverageHeight
                         + MetaGen.CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE;

        var junctions = StreetGeometryHarness.GenerateJunctionsFor(
            city.Cluster, city.Strokes, city.Strokes.GetStreetPoints());

        int nVertices = 0;
        foreach (var mesh in city.ByStroke.Values)
        {
            Assert.All(mesh.Vertices, v => Assert.Equal(expected, v.Y));
            Assert.All(mesh.Normals, nrm => Assert.Equal(Vector3.UnitY, nrm));
            nVertices += mesh.Vertices.Count;
        }

        Assert.True(nVertices > 0);
        Assert.NotEmpty(junctions.Vertices);
        Assert.All(junctions.Vertices, v => Assert.Equal(expected, v.Y));
    }


    /**
     * Only one expression says how high the road is at a junction.
     *
     * Both operators used to write it out for themselves - the block floor under the name
     * MetaGen.ClusterStreetHeight, the road under MetaGen.CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE,
     * two constants that are both 2.0 and are not the same constant, and two of the five
     * copies dropped the deck term as well. A second correct copy passes every test of the
     * VALUE, which is why this is a scan and why it asserts the absence of the old forms as
     * well as the presence of the call.
     *
     * Comments are stripped first. A name in a comment is not a reference, and both files
     * now discuss the constants they no longer use.
     */
    [Fact]
    public void OnlyOneExpressionSaysHowHighTheRoadIsAtAJunction()
    {
        string dir = global::engine.GameRoot.PathTo("JoyceCode") + "/engine/streets";

        foreach (string name in new[]
                 {
                     "GenerateClusterStreetsOperator.cs", "GenerateClusterQuartersOperator.cs"
                 })
        {
            string path = Path.Combine(dir, name);
            Assert.True(File.Exists(path), $"could not find {path}");

            string source = _stripComments(File.ReadAllText(path));

            Assert.Contains("RoadSurface.HeightAtJunction(", source);

            /*
             * Any surviving hand-rolled "ground plus the street offset" in these two files.
             * ClusterStreetHeight has no other business here at all; the road constant is
             * still legitimately used for the flat city's fragment floor plane, which is
             * built from AverageHeight and not from a junction, so that one form is named
             * rather than the constant.
             */
            Assert.DoesNotContain("MetaGen.ClusterStreetHeight", source);
            Assert.DoesNotContain(
                "GroundHeightAt(sp)\n            + world.MetaGen", source);

            foreach (Match m in Regex.Matches(
                         source, @"GroundHeightAt\s*\([^)]*\)\s*\+\s*world\.MetaGen"))
            {
                Assert.Fail($"{name} still computes a junction's road height itself: "
                            + $"'{m.Value}'");
            }
        }
    }


    private static string _stripComments(string source)
    {
        source = Regex.Replace(source, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        return Regex.Replace(source, @"//[^\n]*", " ");
    }
}


/**
 * The rule itself, on a stroke small enough to check by hand.
 *
 * The city-wide gates above measure what the operator emitted, so they can only see the
 * positions the operator happens to emit. Everything a caller may ASK for is here.
 */
public class RoadSurfaceTests
{
    /**
     * One 100 m stroke running along +X, whose right-hand kerb ends 30 m short of the left -
     * a bend at the B junction. hsw is 5 m.
     */
    private static RoadSurface _bent()
        => RoadSurface.Of(
            Vector2.Zero, new Vector2(1f, 0f),
            new Vector2(0f, -5f), new Vector2(0f, 5f),
            new Vector2(100f, -5f), new Vector2(70f, 5f),
            10f, 20f);


    /**
     * Each side reaches the far junction's height at its OWN corner, not at the other's.
     */
    [Fact]
    public void EachSideClimbsBetweenItsOwnTwoCorners()
    {
        var s = _bent();

        Assert.Equal(10f, s.HeightAt(new Vector2(0f, -5f)), 4);
        Assert.Equal(10f, s.HeightAt(new Vector2(0f, 5f)), 4);
        Assert.Equal(20f, s.HeightAt(new Vector2(100f, -5f)), 4);
        Assert.Equal(20f, s.HeightAt(new Vector2(70f, 5f)), 4);

        /*
         * Half way along each kerb, which is a different axial distance on each side.
         */
        Assert.Equal(15f, s.HeightAt(new Vector2(50f, -5f)), 4);
        Assert.Equal(15f, s.HeightAt(new Vector2(35f, 5f)), 4);

        /*
         * ...and the two sides really do disagree in between, or the test above holds for
         * a surface that has no sides at all.
         */
        Assert.NotEqual(
            s.HeightAt(new Vector2(50f, -5f)), s.HeightAt(new Vector2(50f, 5f)), 2);
    }


    /**
     * Beyond either corner the surface stops climbing.
     *
     * A carriageway's emitted vertices all lie between their own side's two corners, so
     * nothing in the shipped city exercises this and removing the clamp changes no geometry
     * at all - it is a bound on what a caller may ASK for, and it is here because that is
     * the only place it can be seen.
     */
    [Fact]
    public void TheSurfaceDoesNotClimbPastEitherCorner()
    {
        var s = _bent();

        Assert.Equal(10f, s.HeightAt(new Vector2(-40f, -5f)), 4);
        Assert.Equal(20f, s.HeightAt(new Vector2(240f, -5f)), 4);
        Assert.Equal(20f, s.HeightAt(new Vector2(240f, 5f)), 4);
    }


    /**
     * A side whose two corners are at the same axial distance has no run to spread the rise
     * over, and takes the near end's height up to that point and the far end's after it.
     *
     * That is the overlapping-footprint case: two junctions so close that there is no
     * carriageway between them.
     */
    [Fact]
    public void ASideWithNoRunStepsRatherThanDividingByZero()
    {
        var s = RoadSurface.Of(
            Vector2.Zero, new Vector2(1f, 0f),
            new Vector2(4f, -5f), new Vector2(0f, 5f),
            new Vector2(4f, -5f), new Vector2(6f, 5f),
            10f, 20f);

        Assert.Equal(10f, s.HeightAt(new Vector2(2f, -5f)), 4);
        Assert.Equal(20f, s.HeightAt(new Vector2(6f, -5f)), 4);
        Assert.False(Single.IsNaN(s.HeightAt(new Vector2(4f, -5f))));
        Assert.Equal(0f, s.SlopeOn(false), 4);
    }


    /**
     * A level stroke says so, which is what keeps the flat city out of the shear entirely.
     */
    [Fact]
    public void ALevelStrokeIsLevel()
    {
        Assert.True(RoadSurface.Of(
            Vector2.Zero, new Vector2(1f, 0f),
            new Vector2(0f, -5f), new Vector2(0f, 5f),
            new Vector2(100f, -5f), new Vector2(70f, 5f),
            10f, 10f).IsLevel);

        Assert.False(_bent().IsLevel);
    }


    /**
     * The normal is perpendicular to the side it belongs to, and the two sides of a bend
     * carry different ones.
     */
    [Fact]
    public void EachSideCarriesItsOwnNormal()
    {
        var s = _bent();

        Vector3 left = s.NormalAt(new Vector2(50f, -5f));
        Vector3 right = s.NormalAt(new Vector2(35f, 5f));

        Assert.Equal(1f, left.Length(), 4);
        Assert.NotEqual(left.X, right.X, 3);

        /*
         * Perpendicular to that side's own tangent: 10 m of rise over its own run.
         */
        Assert.Equal(0f, Vector3.Dot(left, Vector3.Normalize(new Vector3(100f, 10f, 0f))), 4);
        Assert.Equal(0f, Vector3.Dot(right, Vector3.Normalize(new Vector3(70f, 10f, 0f))), 4);
    }


    /**
     * A junction's road height is its ground plus the street offset plus its deck, and
     * nothing else.
     */
    [Fact]
    public void AJunctionsRoadHeightIsItsGroundPlusTheStreetOffsetPlusItsDeck()
    {
        var sp = new StreetPoint() { ClusterId = 0 };
        sp.SetPos(12f, 34f);

        var heights = new FuncStreetHeight((x, z) => 100f);

        Assert.Equal(100f + MetaGen.CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE,
            RoadSurface.HeightAtJunction(heights, sp), 4);

        sp.Level = 1;
        Assert.Equal(
            100f + MetaGen.CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE + StreetLevels.DeckHeight,
            RoadSurface.HeightAtJunction(heights, sp), 4);
    }
}


/**
 * A mesh's triangles, indexed in plan so a surface can be read at a position.
 *
 * Reads the emitted geometry rather than any expression that produced it - which is the
 * whole point when what is being asserted is that two separately emitted surfaces meet.
 */
internal sealed class TriangleField
{
    private const float Cell = 20f;

    /**
     * A point exactly on a shared edge belongs to both triangles, and the road's kerb line
     * IS such an edge - so the barycentric test has to admit the boundary rather than fall
     * between two triangles. Relative to the triangle, so it does not mean something
     * different on a 4 m wedge and a 90 m carriageway row.
     */
    private const float Epsilon = 1e-3f;

    private readonly Dictionary<long, List<int>> _buckets = new();
    private readonly List<(Vector3 a, Vector3 b, Vector3 c)> _tris = new();


    internal static TriangleField Of(Mesh m)
    {
        var f = new TriangleField();
        for (int i = 0; i + 2 < m.Indices.Count; i += 3)
        {
            f._add(m.Vertices[(int)m.Indices[i]],
                   m.Vertices[(int)m.Indices[i + 1]],
                   m.Vertices[(int)m.Indices[i + 2]]);
        }

        return f;
    }


    private void _add(Vector3 a, Vector3 b, Vector3 c)
    {
        int idx = _tris.Count;
        _tris.Add((a, b, c));

        float minX = Single.Min(a.X, Single.Min(b.X, c.X));
        float maxX = Single.Max(a.X, Single.Max(b.X, c.X));
        float minZ = Single.Min(a.Z, Single.Min(b.Z, c.Z));
        float maxZ = Single.Max(a.Z, Single.Max(b.Z, c.Z));

        for (int i = (int)Single.Floor(minX / Cell); i <= (int)Single.Floor(maxX / Cell); ++i)
        for (int k = (int)Single.Floor(minZ / Cell); k <= (int)Single.Floor(maxZ / Cell); ++k)
        {
            long key = ((long)i << 32) ^ (uint)k;
            if (!_buckets.TryGetValue(key, out var l))
            {
                l = new List<int>();
                _buckets[key] = l;
            }

            l.Add(idx);
        }
    }


    /**
     * The height of the surface at a plan position, or null where nothing covers it.
     *
     * The HIGHEST of the triangles covering it, so that a kerb line shared by a carriageway
     * and the junction cap beside it answers with the road rather than with whatever
     * degenerate sliver also touches the point.
     */
    internal float? HeightAt(in Vector2 p)
    {
        long key = ((long)(int)Single.Floor(p.X / Cell) << 32)
                   ^ (uint)(int)Single.Floor(p.Y / Cell);
        if (!_buckets.TryGetValue(key, out var l)) return null;

        float? best = null;
        foreach (int i in l)
        {
            var (a, b, c) = _tris[i];
            Vector2 pa = new(a.X, a.Z), pb = new(b.X, b.Z), pc = new(c.X, c.Z);

            float d = (pb.Y - pc.Y) * (pa.X - pc.X) + (pc.X - pb.X) * (pa.Y - pc.Y);
            if (Single.Abs(d) < 1e-9f) continue;

            float l1 = ((pb.Y - pc.Y) * (p.X - pc.X) + (pc.X - pb.X) * (p.Y - pc.Y)) / d;
            float l2 = ((pc.Y - pa.Y) * (p.X - pc.X) + (pa.X - pc.X) * (p.Y - pc.Y)) / d;
            float l3 = 1f - l1 - l2;
            if (l1 < -Epsilon || l2 < -Epsilon || l3 < -Epsilon) continue;

            float h = l1 * a.Y + l2 * b.Y + l3 * c.Y;
            if (!best.HasValue || h > best.Value) best = h;
        }

        return best;
    }
}
