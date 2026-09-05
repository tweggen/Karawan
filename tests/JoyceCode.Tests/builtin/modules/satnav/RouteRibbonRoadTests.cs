using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using builtin.modules.satnav;
using builtin.modules.satnav.desc;
using engine.joyce;
using engine.navigation;
using engine.streets;
using engine.streets.generation;
using engine.world;
using JoyceCode.Tests.engine.streets;
using Xunit;

namespace JoyceCode.Tests.builtin.modules.satnav;


/**
 * Whether the satnav guideline lies on the road it is drawn on.
 *
 * Reported from play of the terrain-following city as *"the navmesh being partially below
 * the street ... if we draw navmesh streetpoint to streetpoint, without considering the
 * flat junctions, whereas the street level has a flat junction between"* - and the
 * diagnosis is right, which is not the usual outcome on this page.
 *
 * A car NavLane runs from one junction's centre to the next and everything reading it
 * interpolates linearly between the two heights. The road is not that shape: a junction cap
 * is a flat fan at that junction's one height and §7o then gave each side of a stroke its
 * own chord between its own two section points, so the profile along a street is flat,
 * ramp, flat. Chord and profile agree at the two junctions and nowhere in between - above
 * the road over the first cap and below it over the last, by the grade times how far the
 * cap reaches into the stroke.
 *
 * The measurement here is against the road mesh's OWN triangles, read barycentrically over
 * real generated cities on the shipped terrain, and the ribbon is built by the shipping
 * RouteRibbon over lanes built by the shipping GenerateNavMapOperator - not against a
 * recomputed profile, which would only restate the implementation.
 */
public class RouteRibbonRoadTests
{
    /**
     * How far the ribbon may be from the carriageway under it, at the 95th percentile.
     *
     * Not zero, and it is now the two tessellations rather than either model: the road is
     * cut into rows no longer than RoadSurface.MaxRowSpan and the ribbon into quads no
     * longer than RoadSurface.MaxSpanAcross(Width), each of which bounds its own departure
     * from the shared surface at RoadSurface.MaxSag - so the sum is 2 * MaxSag, and that is
     * what is observed.
     *
     * ⚠️ **This bound used to be 0.30 m and the comment on it said "the residual is not
     * the ribbon's". That was wrong, and only measuring both separately showed it.** The
     * road's rows were one texture length - four street widths, up to 88 m - and departed
     * from their own surface by up to 1.0 m mid-row; when that was bounded the guideline
     * was still 0.85 m off the road at the worst position of five cities, every metre of it
     * the ribbon's own long flat quads. See §7s.
     *
     * Observed over the five cities on the shipped terrain: 0.018 / 0.021 / 0.022 / 0.023 /
     * 0.023 m at p95, against 0.16 to 0.25 before and 0.44 to 0.91 for the chord.
     */
    private const float RibbonP95 = 0.035f;

    /**
     * ...and at the 99th. Observed 0.028 to 0.065 m, against 0.24 to 0.46 before and 0.56
     * to 1.33 for the chord.
     */
    private const float RibbonP99 = 0.10f;

    /**
     * The median, which is what the eye reads along a whole route. Observed 0.001 to
     * 0.007 m against 0.002 to 0.021 before and 0.076 to 0.191 for the chord.
     */
    private const float RibbonMedian = 0.015f;

    /**
     * How much worse the chord this replaced has to be before this file can claim to have
     * measured anything. Without it a ribbon collapsed onto one height would satisfy every
     * bound above on a flat city and nothing would say the flat city is the easy case.
     */
    private const float ChordP95AtLeast = 0.40f;


    public static IEnumerable<object[]> Cities()
    {
        yield return new object[] { "seed000", 500f };

        /*
         * The seed whose junction footprints overlap on some strokes - so short that there
         * is no carriageway between them at all, and the two caps then both cover the whole
         * lane. Carried for the same reason KerbSeamTests carries it: the branch is reached
         * by nothing else, so without it a rule about what happens there cannot be broken
         * by any amount of real data.
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
        internal QuarterStore Quarters;
        internal NavClusterContent Content;
        internal Dictionary<Stroke, Mesh> ByStroke;
        internal Dictionary<StreetPoint, TriangleField> Caps = new();

        private readonly Dictionary<Stroke, TriangleField> _fields = new();

        internal TriangleField CarriagewayOf(Stroke stroke)
        {
            if (_fields.TryGetValue(stroke, out var f)) return f;

            f = TriangleField.Of(ByStroke[stroke]);
            _fields[stroke] = f;
            return f;
        }
    }


    private static City _city(string idString, float size, bool flat)
    {
        var cd = StreetHarness.MakeCluster(idString, size);
        cd.AverageHeight = 20f;

        var strokes = StreetHarness.Generate(idString, size);
        cd.StreetHeightSource = flat
            ? new FlatStreetHeight(cd)
            : ShippedTerrain.StreetHeightsOf(cd, strokes);

        var quarters = StreetHarness.GenerateQuarters(cd, strokes, idString);

        var city = new City
        {
            Cluster = cd,
            Strokes = strokes,
            Quarters = quarters,
            ByStroke = StreetGeometryHarness.GenerateEachStroke(cd, strokes),
            Content = GenerateNavMapOperator.ContentOf(cd, strokes, quarters, new NavCluster())
        };

        foreach (var sp in strokes.GetStreetPoints())
        {
            city.Caps[sp] = TriangleField.Of(
                StreetGeometryHarness.GenerateJunctionsFor(cd, strokes, new[] { sp }));
        }

        return city;
    }


    /**
     * Which stroke a car lane runs along.
     *
     * By plan geometry rather than by asking the operator, so that a lane claiming a
     * carriageway it is not on is a thing this file can see. A lane subdivided at
     * MaxLaneLength lies inside its stroke's segment like any other piece of it.
     */
    private static bool _isOn(Stroke stroke, NavLane nl)
        => _isOn(stroke, nl.Start.Position) && _isOn(stroke, nl.End.Position);


    private static bool _isOn(Stroke stroke, in Vector3 p3)
    {
        Vector2 d = new Vector2(p3.X, p3.Z) - stroke.A.Pos;

        return Single.Abs(Vector2.Dot(d, stroke.Normal)) < 0.5f
               && Vector2.Dot(d, stroke.Unit) > -0.5f
               && Vector2.Dot(d, stroke.Unit) < stroke.Length + 0.5f;
    }


    /**
     * How high the road is at a plan position beside one stroke: its own carriageway where
     * that covers the point, and otherwise the cap of whichever of its two junctions does.
     *
     * Not the highest of all three, which is what a combined mesh would answer. Where two
     * junction footprints OVERLAP - seed008 has such strokes - both caps cover the same
     * ground at two different heights, and "how high is the road here" genuinely has two
     * answers there (§7o recorded the same thing for two overlapping carriageways). Those
     * positions are counted rather than folded into the error.
     */
    private static float? _roadUnder(City city, Stroke stroke, in Vector2 p, ref int nAmbiguous)
    {
        float? own = city.CarriagewayOf(stroke).HeightAt(p);
        if (own.HasValue) return own;

        float? a = city.Caps[stroke.A].HeightAt(p);
        float? b = city.Caps[stroke.B].HeightAt(p);

        if (a.HasValue && b.HasValue)
        {
            if (Single.Abs(a.Value - b.Value) > 0.02f)
            {
                ++nAmbiguous;
                return null;
            }

            return a;
        }

        return a ?? b;
    }


    private static float _pct(List<float> v, float p)
    {
        var s = v.OrderBy(x => x).ToList();
        int i = (int)Single.Round(p * (s.Count - 1));

        return s[Math.Clamp(i, 0, s.Count - 1)];
    }


    /**
     * The mesh of one lane's ribbon, so that what is measured is the surface a player sees
     * rather than the corner heights it was built from.
     */
    private static TriangleField _ribbonOf(
        NavLane nl, TransportationType tt, List<RouteRibbon.Quad> quads, List<float> breaks)
    {
        RouteRibbon.QuadsFor(nl, tt, quads, breaks);

        var m = Mesh.CreateNormalsListInstance("ribbon");
        foreach (var q in quads)
        {
            global::engine.joyce.mesh.Tools.AddQuadCornersUV(
                m, q.V00, q.V10, q.V01, q.V11, Vector2.Zero, Vector2.Zero, Vector2.Zero);
        }

        return TriangleField.Of(m);
    }


    /**
     * THE gate: the guideline lies on the carriageway, all the way along every car lane and
     * all the way across its own width.
     *
     * The same samples are measured against the chord the ribbon used to be, so that this
     * cannot be satisfied by anything which merely happens to agree where it is looked at.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void TheRibbonLiesOnTheCarriagewayAlongEveryCarLane(string idString, float size)
    {
        var city = _city(idString, size, false);

        var quads = new List<RouteRibbon.Quad>();
        var breaks = new List<float>();

        var devRibbon = new List<float>();
        var devChord = new List<float>();
        int nAmbiguous = 0, nUncovered = 0, nBuried = 0;
        float worst = 0f;
        string worstWhere = "";

        var carLanes = city.Content.Lanes
            .Where(l => l.AllowedTypes.HasFlag(TransportationType.Car)).ToList();

        foreach (var stroke in city.Strokes.GetStrokes())
        {
            foreach (var nl in carLanes.Where(l => _isOn(stroke, l)))
            {
                var ribbon = _ribbonOf(nl, TransportationType.Car, quads, breaks);

                Vector3 a = nl.Start.Position;
                Vector3 along = nl.End.Position - a;
                Vector3 right = Vector3.Normalize(new Vector3(along.Z, 0f, -along.X));

                float chordA = RouteRibbon.SurfaceHeightOf(nl.Start, TransportationType.Car);
                float chordB = RouteRibbon.SurfaceHeightOf(nl.End, TransportationType.Car);

                for (int k = 0; k <= 20; ++k)
                {
                    float t = k / 20f;
                    for (int j = -2; j <= 2; ++j)
                    {
                        Vector3 p3 = a + t * along + (j * 0.9f) * right;
                        Vector2 p = new(p3.X, p3.Z);

                        float? road = _roadUnder(city, stroke, p, ref nAmbiguous);
                        float? drawn = ribbon.HeightAt(p);
                        if (!road.HasValue || !drawn.HasValue)
                        {
                            ++nUncovered;
                            continue;
                        }

                        float d = drawn.Value - RouteRibbon.Lift - road.Value;
                        devRibbon.Add(Single.Abs(d));
                        devChord.Add(Single.Abs(Single.Lerp(chordA, chordB, t) - road.Value));

                        if (d < -RouteRibbon.Lift) ++nBuried;
                        if (Single.Abs(d) > worst)
                        {
                            worst = Single.Abs(d);
                            worstWhere = $"stroke {stroke.Sid}, {t:F2} along a lane, at {p}";
                        }
                    }
                }
            }
        }

        Assert.True(devRibbon.Count > 1000,
            $"{idString}/{size}: only {devRibbon.Count} positions were measurable");

        Assert.True(_pct(devRibbon, 0.5f) < RibbonMedian,
            $"{idString}/{size}: the guideline is {_pct(devRibbon, 0.5f):F4} m off the road "
            + $"at the MEDIAN; worst {worst:F3} m at {worstWhere}");
        Assert.True(_pct(devRibbon, 0.95f) < RibbonP95,
            $"{idString}/{size}: the guideline is {_pct(devRibbon, 0.95f):F4} m off the road "
            + $"at p95; worst {worst:F3} m at {worstWhere}");
        Assert.True(_pct(devRibbon, 0.99f) < RibbonP99,
            $"{idString}/{size}: the guideline is {_pct(devRibbon, 0.99f):F4} m off the road "
            + $"at p99; worst {worst:F3} m at {worstWhere}");

        /*
         * The lift exists to keep the ribbon out of the road, not to excuse being wrong by
         * less than itself - so this counts the positions where it is used up entirely.
         * Observed 0.00 to 0.71 %, against 3.2 to 9.5 % before the two tessellations were
         * bounded and 44 to 51 % of positions simply below the road for the chord.
         */
        Assert.True(nBuried * 50 < devRibbon.Count,
            $"{idString}/{size}: the guideline is more than its own lift below the road at "
            + $"{100f * nBuried / devRibbon.Count:F1} % of positions");

        /*
         * ...and the chord it replaced was much worse on exactly these samples.
         */
        Assert.True(_pct(devChord, 0.95f) > ChordP95AtLeast,
            $"{idString}/{size}: the chord is only {_pct(devChord, 0.95f):F4} m off the road "
            + "at p95, so this measurement cannot tell the two apart");
        Assert.True(_pct(devChord, 0.95f) > 2.5f * _pct(devRibbon, 0.95f),
            $"{idString}/{size}: the chord is {_pct(devChord, 0.95f):F4} m off at p95 and "
            + $"the ribbon {_pct(devRibbon, 0.95f):F4} m, which is not an improvement worth "
            + "the machinery");
    }


    /**
     * The claim the whole fix rests on: the surface a lane carries IS the road that was
     * emitted, vertex for vertex.
     *
     * Asserted at the road mesh's own vertices rather than at sampled positions, because
     * that is where the two can be compared with nothing interpolating in between - and it
     * is what makes the residual in the gate above attributable to the road's tessellation
     * rather than to the surface.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void TheCarriagewaySurfaceReproducesEveryVertexOfTheRoadItDescribes(
        string idString, float size)
    {
        var city = _city(idString, size, false);

        var errors = new List<float>();
        var skewErrors = new List<float>();
        int nSkewStrokes = 0, nStrokes = 0;

        foreach (var stroke in city.Strokes.GetStrokes())
        {
            var surface = RoadSurface.OfStroke(
                stroke, city.Cluster.StreetHeightSource, Vector2.Zero);
            Assert.True(surface.HasValue,
                $"{idString}/{size}: stroke {stroke.Sid} emits a carriageway but has none");

            ++nStrokes;
            bool skew = _isSkew(stroke);
            if (skew) ++nSkewStrokes;

            foreach (var v in city.ByStroke[stroke].Vertices)
            {
                float e = Single.Abs(
                    surface.Value.SurfaceHeightAt(new Vector2(v.X, v.Z)) - v.Y);
                (skew ? skewErrors : errors).Add(e);
            }
        }

        Assert.True(errors.Count > 300, $"{idString}/{size}: {errors.Count} vertices is too few");

        /*
         * On every stroke whose section points ARE half a street width off its centre line,
         * the surface reproduces every emitted vertex - not at p99 but at the maximum.
         * Observed 0.00004 m at the worst of 63 000 such vertices over five cities.
         */
        Assert.Equal(0f, _pct(errors, 0.5f), 5);
        Assert.True(_pct(errors, 1f) < 1e-3f,
            $"{idString}/{size}: the surface is {_pct(errors, 1f):F6} m off the road's own "
            + "vertex on a stroke whose kerbs are where its rows are");

        /*
         * ⚠️ **The rest is §7o's own recorded defect and this test used to hide it behind a
         * percentile.** A handful of strokes have a section point that is not half a street
         * width off the centre line - §7o measured one of Yelukhdidru/3000's at 0.19 m
         * inside - while the ROWS are emitted at exactly plus or minus half a width, so on
         * those strokes the mesh's own rows are not on the mesh's own kerb chords and the
         * surface has to blend the two sides a little at a vertex. The count is small (0, 2,
         * 0, 8 and 12 strokes of 29, 30, 74, 367 and 1875) and does not grow, but the number
         * of VERTICES carrying it does - §7s cut the rows finer, which put more vertices on
         * the same wrong lines and pushed the old p99 past its bound without anything having
         * become less true. Named and bounded instead of averaged away.
         */
        Assert.True(nSkewStrokes * 10 <= nStrokes,
            $"{idString}/{size}: {nSkewStrokes} of {nStrokes} strokes have a section point "
            + "that is not half a street width off their own centre line");

        if (skewErrors.Count > 0)
        {
            Assert.True(_pct(skewErrors, 1f) < 0.25f,
                $"{idString}/{size}: the surface is {_pct(skewErrors, 1f):F4} m off a road "
                + "vertex even on a stroke whose kerbs are not where its rows are");
        }
    }


    /**
     * Whether a stroke's four section points sit at exactly plus or minus half its street
     * width off its own centre line, which is where its rows are emitted.
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
     * A lane's carriageway is bounded by section points of its own two junctions.
     *
     * On identity, not on distance: two arms of a junction can be near-collinear and a
     * neighbouring junction can be closer to a corner than its own, so a metric test here
     * proves nothing. A stroke that is the only arm at one of its junctions has no section
     * array there and is skipped at that end, which is the same rule the emission uses.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void TheCarriagewaysCornersAreSectionPointsOfItsOwnJunctions(
        string idString, float size)
    {
        var city = _city(idString, size, false);

        int nChecked = 0;

        foreach (var stroke in city.Strokes.GetStrokes())
        {
            Assert.True(RoadSurface.TryCornersOf(
                stroke, out var al, out var ar, out var bl, out var br, out var why), why);

            if (stroke.A.GetAngleArray().Count > 1)
            {
                Assert.Contains(al, stroke.A.GetSectionArray());
                Assert.Contains(ar, stroke.A.GetSectionArray());
                Assert.NotEqual(al, ar);
                nChecked += 2;
            }

            if (stroke.B.GetAngleArray().Count > 1)
            {
                Assert.Contains(bl, stroke.B.GetSectionArray());
                Assert.Contains(br, stroke.B.GetSectionArray());
                Assert.NotEqual(bl, br);
                nChecked += 2;
            }
        }

        Assert.True(nChecked > 30, $"{idString}/{size}: {nChecked} corners is too few");
    }


    /**
     * A ribbon sits exactly on its own junction wherever the road does - and where it does
     * not, that is characterised rather than tolerated.
     *
     * A junction cap is a flat fan at one height and RoadSurface clamps each side's chord
     * fraction at that side's own section point, so a position over the cap gets the
     * junction's own height and the two expressions are the SAME FLOAT. That is what makes
     * this a change to the middle of a lane and not to its ends.
     *
     * The exception is exact, not a tolerance: a section point can project BEHIND its own
     * junction's centre, which is what happens at a sharp bend - two arms 30 degrees apart
     * put the outer mitre 3.7 street half-widths back up the stroke - and the carriageway
     * there is the wedge triangle filling the notch, not the cap. So the equality is
     * asserted exactly where it must hold - the position's axial coordinate at or before
     * BOTH of that junction's section points - and the rest are counted, over whole
     * cities, rather than the difference being bounded by a tolerance that hides both.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void ARibbonMeetsItsOwnJunctionExactlyWhereTheCapReachesIt(
        string idString, float size)
    {
        var city = _city(idString, size, false);

        var quads = new List<RouteRibbon.Quad>();
        var breaks = new List<float>();
        int nOnTheCap = 0, nOnTheWedge = 0;

        var carLanes = city.Content.Lanes
            .Where(l => l.AllowedTypes.HasFlag(TransportationType.Car)).ToList();

        foreach (var stroke in city.Strokes.GetStrokes())
        {
            var surface = RoadSurface.OfStroke(
                stroke, city.Cluster.StreetHeightSource, Vector2.Zero).Value;

            Assert.True(RoadSurface.TryCornersOf(
                stroke, out var al, out var ar, out var bl, out var br, out _));

            float dA = Single.Min(surface.AxialAt(al), surface.AxialAt(ar));
            float dB = Single.Max(surface.AxialAt(bl), surface.AxialAt(br));

            foreach (var nl in carLanes.Where(l => _isOn(stroke, l)))
            {
                RouteRibbon.QuadsFor(nl, TransportationType.Car, quads, breaks);

                foreach (var (nj, corner) in new[]
                         {
                             (nl.Start, quads[0].V00), (nl.Start, quads[0].V10),
                             (nl.End, quads[^1].V01), (nl.End, quads[^1].V11)
                         })
                {
                    Vector2 p = new(corner.X, corner.Z);
                    if (!_isJunction(stroke, p, out StreetPoint sp)) continue;

                    float d = surface.AxialAt(p);
                    bool onTheCap = sp == stroke.A ? d <= dA : d >= dB;

                    float own = RouteRibbon.SurfaceHeightOf(nj, TransportationType.Car)
                                + RouteRibbon.Lift;

                    if (onTheCap)
                    {
                        Assert.Equal(own, corner.Y);
                        ++nOnTheCap;
                    }
                    else
                    {
                        /*
                         * Counted, not asserted to differ: a stroke the relaxer levelled
                         * has one height at both ends, so the wedge carries the junction's
                         * height too and there is nothing here to distinguish.
                         */
                        ++nOnTheWedge;
                    }
                }
            }
        }

        Assert.True(nOnTheCap > 40,
            $"{idString}/{size}: only {nOnTheCap} lane corners stand on a junction cap");
        Assert.True(nOnTheWedge < nOnTheCap,
            $"{idString}/{size}: {nOnTheWedge} of {nOnTheCap + nOnTheWedge} corners at a "
            + "junction are on the wedge behind its section points rather than on its cap");
    }


    /**
     * Whether a plan position is at one of a stroke's two junctions, to within the 0.1 m
     * grid StreetPoint.SetPos quantises junction positions onto plus the ribbon's own half
     * width across.
     */
    private static bool _isJunction(Stroke stroke, in Vector2 p, out StreetPoint sp)
    {
        sp = null;
        foreach (var candidate in new[] { stroke.A, stroke.B })
        {
            Vector2 d = p - candidate.Pos;
            if (Single.Abs(Vector2.Dot(d, stroke.Unit)) > 0.2f) continue;
            if (Single.Abs(Vector2.Dot(d, stroke.Normal)) > RouteRibbon.Width) continue;

            sp = candidate;
            return true;
        }

        return false;
    }


    /**
     * ⚠️ THE FLAT CITY DOES NOT MOVE, and it is asserted as equality rather than assumed.
     *
     * All of a flat city's junctions are at one height, so its carriageway is level, has no
     * break in it and gets none - one quad per lane, exactly as before - and every corner
     * takes the same float the chord gave it. Vertex for vertex, index for index.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void AFlatCitysRibbonIsUnchangedFloatForFloat(string idString, float size)
    {
        var city = _city(idString, size, true);

        var quads = new List<RouteRibbon.Quad>();
        var breaks = new List<float>();
        int nChecked = 0;

        foreach (var nl in city.Content.Lanes)
        {
            var tt = nl.AllowedTypes.HasFlag(TransportationType.Car)
                ? TransportationType.Car
                : TransportationType.Pedestrian;

            RouteRibbon.QuadsFor(nl, tt, quads, breaks);

            Assert.Single(quads);

            /*
             * The construction the ribbon replaced: a corner half a width to one side, one
             * width across, and the difference of the two junctions' own surface points
             * along. Written out here so the two are compared through separate expressions.
             */
            Vector3 v3Start = RouteRibbon.PointOn(nl.Start, tt);
            Vector3 v3End = RouteRibbon.PointOn(nl.End, tt);
            Vector3 v3Plan = nl.End.Position - nl.Start.Position;
            Vector3 vu3Right = Vector3.Normalize(new Vector3(v3Plan.Z, 0f, -v3Plan.X));

            Vector3 v3Origin = v3Start + (RouteRibbon.Width / 2f) * vu3Right;
            Vector3 v3Across = -RouteRibbon.Width * vu3Right;
            Vector3 v3Along = v3End - v3Start;

            Assert.Equal(v3Origin, quads[0].V00);
            Assert.Equal(v3Origin + v3Across, quads[0].V10);
            Assert.Equal(v3Origin + v3Along, quads[0].V01);
            Assert.Equal(v3Origin + v3Across + v3Along, quads[0].V11);
            ++nChecked;
        }

        Assert.True(nChecked > 100, $"{idString}/{size}: {nChecked} lanes is too few");
    }


    /**
     * A lane's breaks include BOTH of the ribbon's edges' own crossings of each seam.
     *
     * A junction's seam runs across the road at an angle, so the two edges of a 4 m strip
     * cross it at two different distances along the stroke, and a quad that bends at only
     * one of them ramps straight through the kink on the other edge. Measured over the five
     * cities that one missing break was the whole tail of the guideline's error - 0.85 m at
     * the worst position, 0.22 m at p99 - and yet **giving both edges the same lateral
     * fraction passed every distribution in this file**, because the tail is 0.1 % of
     * positions. So it is asserted here as equality with the two exact crossings, on a
     * fixture where they are 4 m apart: §7p's *"a containment test cannot tell a guess from
     * a refusal"*.
     */
    [Fact]
    public void ALanesBreaksIncludeBothEdgesOwnCrossingOfEachSeam()
    {
        /*
         * A 10 m carriageway from x = 0 to x = 100, bent at both ends: its two A section
         * points are 10 m apart along the stroke and so are its two B ones.
         */
        var surface = RoadSurface.Of(
            Vector2.Zero, Vector2.UnitX,
            new Vector2(10f, -5f), new Vector2(20f, 5f),
            new Vector2(90f, -5f), new Vector2(80f, 5f),
            20f, 50f);

        var nl = new NavLane
        {
            Start = NavJunction.At(Vector3.Zero, 0f),
            End = NavJunction.At(new Vector3(100f, 0f, 0f), 0f),
            Length = 100f,
            Surface = surface
        };

        var breaks = new List<float>();
        RouteRibbon.BreaksAlong(nl, breaks);

        /*
         * The ribbon is 4 m of a 10 m road, so its two edges are at lateral fractions 0.3
         * and 0.7 - and each seam runs from one section point to the other, so they cross it
         * at 13 and 17 m, and at 87 and 83 m.
         */
        foreach (float d in new[] { 13f, 17f, 83f, 87f })
        {
            Assert.True(breaks.Any(t => Single.Abs(t - d / 100f) < 1e-3f),
                $"no break at {d} m, where one edge of the ribbon crosses a seam; "
                + "breaks are at " + string.Join(", ", breaks.Select(t => $"{t * 100f:F2}")));
        }
    }


    /**
     * A bent stroke's ribbon bends with the road; a level one is a single quad.
     *
     * The count is a property worth pinning because it is what a "just draw one quad and
     * take the surface height at its corners" shortcut would quietly lose: the corners
     * would be right and everything between them would cut straight across the profile.
     *
     * ⚠️ **This test used to require at most 5 quads per lane, on the stated grounds that
     * "a stroke has only four section points to break at". Both halves are now wrong** -
     * see §7s. A lane also breaks where each of the ribbon's two EDGES crosses a junction's
     * seam, which is not a section point and is a different distance for each edge, and
     * then again as often as it takes to keep its own quads within RoadSurface.MaxSag of
     * the twisted surface they are drawn on. Observed: median 4 quads per lane, p95 5 to 9,
     * max 40 - against a median of 3 and a max of 5. The bound is now the arithmetic cap,
     * and it is asserted NOT to bind.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void ALaneOverAClimbingRoadIsBrokenWhereTheRoadBreaks(string idString, float size)
    {
        var city = _city(idString, size, false);

        var quads = new List<RouteRibbon.Quad>();
        var breaks = new List<float>();

        int nBroken = 0, nCar = 0, nTotalQuads = 0;

        foreach (var nl in city.Content.Lanes)
        {
            if (!nl.AllowedTypes.HasFlag(TransportationType.Car)) continue;

            ++nCar;
            RouteRibbon.QuadsFor(nl, TransportationType.Car, quads, breaks);
            nTotalQuads += quads.Count;
            if (quads.Count > 1) ++nBroken;

            /*
             * Strictly under the cap, not at it: RouteRibbon.MaxQuadsPerLane exists so that
             * a stroke whose two section points nearly coincide cannot ask for an unbounded
             * number, and a city that reaches it is a city whose ribbon is silently coarser
             * than MaxSag rather than one that is merely expensive.
             */
            Assert.True(quads.Count < RouteRibbon.MaxQuadsPerLane,
                $"{idString}/{size}: a lane came out in {quads.Count} quads, which is the "
                + "arithmetic cap - so the bound on its own sag was not what decided it");
        }

        /*
         * Every stroke of a terrain-following city climbs, so most of its lanes carry at
         * least one break. The ones that do not are the pieces of a subdivided lane that
         * lie wholly between the section points, where the road is one straight ramp.
         */
        Assert.True(nBroken * 2 > nCar,
            $"{idString}/{size}: only {nBroken} of {nCar} car lanes bend with the road");

        /*
         * What it costs, in the currency the report is paid in: four vertices per quad.
         * Observed 3.1 to 4.6 quads per lane over the five cities, against 2.1 to 2.5
         * before the ribbon bounded its own sag. A whole 3 km city's car lanes come to
         * 33 539 quads; a ROUTE is a few dozen lanes, so a guideline costs a few hundred
         * vertices.
         */
        Assert.True(nTotalQuads < 6 * nCar,
            $"{idString}/{size}: {nTotalQuads} quads for {nCar} lanes is more than the "
            + "profile has corners");
    }


    /**
     * A pedestrian lane does NOT have this defect, and the difference is not a nuance.
     *
     * A pavement lane runs from one block corner to the next, each at its own junction's
     * height, and the block floor's outline is the straight segment between exactly those
     * two heights - so the chord IS the kerb line, identically. Asserted as equality over
     * every sidewalk lane of a real city rather than argued.
     *
     * A CROSSING is a different thing again: it spans the carriageway between two section
     * points of one junction, both at that junction's height, so it is level and stands one
     * kerb above the cap it crosses - which is where a walker's feet go and is deliberate.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void APedestrianRibbonIsAlreadyOnItsOwnPavement(string idString, float size)
    {
        var city = _city(idString, size, false);

        var outlines = new Dictionary<Quarter, List<Vector3>>();
        foreach (var q in city.Quarters.GetQuarters())
        {
            if (q.IsInvalid()) continue;
            outlines[q] = GenerateClusterQuartersOperator.FloorOutlineOf(q, 0f, 0f);
        }

        int nChecked = 0;
        float worst = 0f;

        foreach (var nl in city.Content.Lanes)
        {
            if (nl.AllowedTypes.HasFlag(TransportationType.Car)) continue;

            /*
             * A crossing carries no kerb side, by construction - it is in the roadway.
             */
            if (nl.KerbSide == Vector3.Zero) continue;

            Assert.Null(nl.Surface);

            foreach (var (_, outline) in outlines)
            {
                if (!_edgeUnder(outline, nl, out Vector3 c0, out Vector3 c1,
                        out float ta, out float tb)) continue;

                for (int k = 0; k <= 10; ++k)
                {
                    float t = k / 10f;
                    Vector3 p3 = Vector3.Lerp(nl.Start.Position, nl.End.Position, t);

                    float drawn = RouteRibbon.SurfaceHeightAt(
                        nl, p3, t, TransportationType.Pedestrian);
                    float pavement = Single.Lerp(c0.Y, c1.Y, ta + t * (tb - ta))
                                     + MetaGen.QuarterSidewalkOffset;

                    worst = Single.Max(worst, Single.Abs(drawn - pavement));
                }

                ++nChecked;
                break;
            }
        }

        Assert.True(nChecked > 20,
            $"{idString}/{size}: only {nChecked} sidewalk lanes were matched to a block edge");

        /*
         * Single precision on cluster coordinates of up to 1500 m, not a fitted bound:
         * observed worst 2.3e-5 m over the five cities.
         */
        Assert.True(worst < 1e-3f,
            $"{idString}/{size}: a pavement ribbon is {worst:F6} m off the block floor it "
            + "is drawn on");
    }


    /**
     * Which block edge, if any, a sidewalk lane runs along, and where its two ends project
     * onto it.
     */
    private static bool _edgeUnder(
        List<Vector3> outline, NavLane nl,
        out Vector3 c0, out Vector3 c1, out float ta, out float tb)
    {
        c0 = c1 = Vector3.Zero;
        ta = tb = 0f;

        Vector2 a = new(nl.Start.Position.X, nl.Start.Position.Z);
        Vector2 b = new(nl.End.Position.X, nl.End.Position.Z);

        int n = outline.Count;
        for (int i = 0; i < n; ++i)
        {
            c0 = outline[i];
            c1 = outline[(i + 1) % n];
            Vector2 p0 = new(c0.X, c0.Z), p1 = new(c1.X, c1.Z);
            Vector2 e = p1 - p0;
            float l2 = e.LengthSquared();
            if (l2 < 1f) continue;

            ta = Vector2.Dot(a - p0, e) / l2;
            tb = Vector2.Dot(b - p0, e) / l2;

            if ((a - (p0 + ta * e)).Length() > 0.2f) continue;
            if ((b - (p0 + tb * e)).Length() > 0.2f) continue;
            if (ta < -0.01f || ta > 1.01f || tb < -0.01f || tb > 1.01f) continue;

            return true;
        }

        return false;
    }


    /**
     * ...and it stays on the pavement even when handed a carriageway.
     *
     * Nothing shipped puts a road surface on a pedestrian lane, which is exactly why this
     * is a fixture: a guard whose condition no real data can make false is a guard that can
     * be deleted without any city noticing, and this one decides whether a walking route is
     * drawn on the pavement or sunk one kerb into the slab it is drawn on.
     */
    [Fact]
    public void APedestrianRibbonIgnoresACarriagewayEvenIfItIsGivenOne()
    {
        var njA = NavJunction.At(new Vector3(0f, 0f, 0f), 10f);
        var njB = NavJunction.At(new Vector3(100f, 0f, 0f), 22f);

        var nl = new NavLane
        {
            Start = njA,
            End = njB,
            Length = 100f,
            AllowedTypes = new TransportationTypeFlags(TransportationType.Pedestrian),
            Surface = RoadSurface.Of(
                Vector2.Zero, new Vector2(1f, 0f),
                new Vector2(20f, -6f), new Vector2(20f, 6f),
                new Vector2(80f, -6f), new Vector2(80f, 6f),
                12f, 24f)
        };

        Vector3 mid = new(50f, 0f, 0f);

        Assert.Equal(
            Single.Lerp(NavJunction.WalkingHeightOf(10f), NavJunction.WalkingHeightOf(22f), 0.5f),
            RouteRibbon.SurfaceHeightAt(nl, mid, 0.5f, TransportationType.Pedestrian), 4);
    }


    /**
     * Every quad of every lane reaches the mesh.
     *
     * Mutation testing found that drawing only the first quad of each lane passed the whole
     * suite: the corners stay right and only what is between them is lost, and the emission
     * used to live at a call site that needs a booted engine and is covered by a scan. So
     * the count is asserted against what the ribbon itself says it produced, over a real
     * city, and the mesh is built by the same method the game builds it with.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void EveryQuadOfEveryLaneReachesTheMesh(string idString, float size)
    {
        var city = _city(idString, size, false);

        var lanes = city.Content.Lanes
            .Where(l => l.AllowedTypes.HasFlag(TransportationType.Car)).ToList();

        var quads = new List<RouteRibbon.Quad>();
        var breaks = new List<float>();

        int nExpected = 0;
        foreach (var nl in lanes)
        {
            RouteRibbon.QuadsFor(nl, TransportationType.Car, quads, breaks);
            nExpected += quads.Count;
        }

        var mesh = RouteRibbon.MeshFor(lanes, TransportationType.Car);

        Assert.True(nExpected > lanes.Count,
            $"{idString}/{size}: {nExpected} quads for {lanes.Count} lanes means no lane "
            + "follows the road at all");
        Assert.Equal(4 * nExpected, mesh.Vertices.Count);
        Assert.Equal(6 * nExpected, mesh.Indices.Count);

        /*
         * Vertex for vertex and in order, not merely the right number of them: a swapped
         * pair of corners keeps the count, turns each quad inside out and faces it away
         * from the camera - which is §7j's culled pavements in a mesh four vertices wide.
         */
        int at = 0;
        foreach (var nl in lanes)
        {
            RouteRibbon.QuadsFor(nl, TransportationType.Car, quads, breaks);
            foreach (var q in quads)
            {
                Assert.Equal(q.V00, mesh.Vertices[at + 0]);
                Assert.Equal(q.V10, mesh.Vertices[at + 1]);
                Assert.Equal(q.V01, mesh.Vertices[at + 2]);
                Assert.Equal(q.V11, mesh.Vertices[at + 3]);
                at += 4;
            }
        }

        /*
         * ...and the mesh is built for the network it was asked about. A pavement ribbon
         * stands one kerb above the carriageway, so a mesh that quietly drew every route as
         * a car route would be sunk into the slab it is drawn on.
         */
        var onFoot = RouteRibbon.MeshFor(
            city.Content.Lanes
                .Where(l => !l.AllowedTypes.HasFlag(TransportationType.Car)).ToList(),
            TransportationType.Pedestrian);

        var driving = RouteRibbon.MeshFor(
            city.Content.Lanes
                .Where(l => !l.AllowedTypes.HasFlag(TransportationType.Car)).ToList(),
            TransportationType.Car);

        Assert.Equal(onFoot.Vertices.Count, driving.Vertices.Count);
        for (int i = 0; i < onFoot.Vertices.Count; ++i)
        {
            Assert.Equal(MetaGen.QuarterSidewalkOffset,
                onFoot.Vertices[i].Y - driving.Vertices[i].Y, 4);
        }
    }


    /**
     * The ribbon's triangles still face upwards.
     *
     * §7j found half a hillside city's pavements culled away with a complete mesh, no
     * exception and nothing in the log, because a winding flipped - so a change to how the
     * ribbon's quads are emitted says which way they point, rather than being assumed to
     * have kept it.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void TheRibbonFacesUpwards(string idString, float size)
    {
        var city = _city(idString, size, false);

        var quads = new List<RouteRibbon.Quad>();
        var breaks = new List<float>();
        int nChecked = 0;

        foreach (var nl in city.Content.Lanes)
        {
            if (!nl.AllowedTypes.HasFlag(TransportationType.Car)) continue;

            RouteRibbon.QuadsFor(nl, TransportationType.Car, quads, breaks);

            foreach (var q in quads)
            {
                foreach (var (a, b, c) in new[]
                         {
                             (q.V00, q.V10, q.V01), (q.V01, q.V10, q.V11)
                         })
                {
                    float up = Vector3.Cross(b - a, c - a).Y;
                    Assert.True(up > 0f,
                        $"{idString}/{size}: a ribbon triangle at {a} faces {up}, i.e. away "
                        + "from the camera that has to see it");
                    ++nChecked;
                }
            }
        }

        Assert.True(nChecked > 200, $"{idString}/{size}: {nChecked} triangles is too few");
    }


    /**
     * ⚠️ A city away from the origin keeps its ribbon on its road, and no baseline can say
     * so.
     *
     * Every generated city this file measures sits at ClusterDesc.Pos = zero, so dropping
     * the cluster offset from RoadSurface.OfStroke - which asks a carriageway in cluster
     * coordinates about a junction in WORLD ones, and answers with whichever end of the
     * street the offset lands past - passes every one of them. §7n's lesson in a new coat:
     * a rule that no data available can make false is a rule that can be deleted.
     *
     * The shipped world puts 70 cities at a median 36 km from the origin, so this is the
     * ordinary case and the baselines are the exception.
     */
    [Fact]
    public void ACityAwayFromTheOriginKeepsItsRibbonOnItsRoad()
    {
        const string Seed = "seed000";
        const float Size = 1500f;
        Vector3 v3Far = new(1234f, 0f, -567f);
        Vector2 v2Far = new(v3Far.X, v3Far.Z);

        var cd = StreetHarness.MakeCluster(Seed, Size);
        cd.AverageHeight = 20f;
        cd.Pos = v3Far;

        var strokes = StreetHarness.Generate(Seed, Size);
        cd.StreetHeightSource = ShippedTerrain.StreetHeightsOf(cd, strokes);

        var city = new City
        {
            Cluster = cd,
            Strokes = strokes,
            Quarters = StreetHarness.GenerateQuarters(cd, strokes, Seed),
            ByStroke = StreetGeometryHarness.GenerateEachStroke(cd, strokes)
        };
        city.Content = GenerateNavMapOperator.ContentOf(
            cd, strokes, city.Quarters, new NavCluster());

        foreach (var sp in strokes.GetStreetPoints())
        {
            city.Caps[sp] = TriangleField.Of(
                StreetGeometryHarness.GenerateJunctionsFor(cd, strokes, new[] { sp }));
        }

        var quads = new List<RouteRibbon.Quad>();
        var breaks = new List<float>();
        var dev = new List<float>();
        int nAmbiguous = 0;

        var carLanes = city.Content.Lanes
            .Where(l => l.AllowedTypes.HasFlag(TransportationType.Car)).ToList();

        foreach (var stroke in strokes.GetStrokes())
        {
            /*
             * The mesh is emitted in CLUSTER coordinates - the operator is handed the
             * cluster's offset from the fragment being built - while a NavJunction is in
             * world space. So the samples come back to the mesh's space here, which is the
             * one place in this file where the two are told apart.
             */
            foreach (var nl in carLanes.Where(l => _isOn(stroke, _shifted(l, -v3Far))))
            {
                RouteRibbon.QuadsFor(nl, TransportationType.Car, quads, breaks);

                foreach (var q in quads)
                {
                    foreach (var corner in new[] { q.V00, q.V10, q.V01, q.V11 })
                    {
                        Vector2 p = new Vector2(corner.X, corner.Z) - v2Far;

                        float? road = _roadUnder(city, stroke, p, ref nAmbiguous);
                        if (!road.HasValue) continue;

                        dev.Add(Single.Abs(corner.Y - RouteRibbon.Lift - road.Value));
                    }
                }
            }
        }

        Assert.True(dev.Count > 1000,
            $"only {dev.Count} corners of a city 1.4 km out were measurable");
        Assert.True(_pct(dev, 0.95f) < RibbonP95,
            $"a city 1.4 km from the origin has its guideline {_pct(dev, 0.95f):F4} m off "
            + $"the road at p95, worst {_pct(dev, 1f):F3} m");
    }


    /**
     * A lane moved back into its cluster's own coordinates, for comparison against a mesh
     * that was emitted there.
     */
    private static NavLane _shifted(NavLane nl, in Vector3 v3By) => new()
    {
        Start = NavJunction.At(nl.Start.Position + v3By, nl.Start.GroundHeight),
        End = NavJunction.At(nl.End.Position + v3By, nl.End.GroundHeight)
    };


    /**
     * A carriageway with no width across it still answers a height rather than a NaN.
     *
     * Unreachable from a generated city - every street has a width - which is why it is a
     * fixture and why the assertion is EQUALITY with the height the two coincident kerbs
     * carry, not merely that the answer is finite. A guess lands on a plausible number as
     * readily as a refusal does; §7p.
     */
    [Fact]
    public void ACarriagewayWithNoWidthStillAnswersItsOwnHeight()
    {
        var surface = RoadSurface.Of(
            Vector2.Zero, new Vector2(1f, 0f),
            new Vector2(10f, 0f), new Vector2(10f, 0f),
            new Vector2(90f, 0f), new Vector2(90f, 0f),
            30f, 50f);

        Assert.Equal(30f, surface.SurfaceHeightAt(new Vector2(10f, 0f)));
        Assert.Equal(50f, surface.SurfaceHeightAt(new Vector2(90f, 0f)));
        Assert.Equal(40f, surface.SurfaceHeightAt(new Vector2(50f, 0f)), 4);

        /*
         * ...and over the cap at either end, where the chord fraction clamps.
         */
        Assert.Equal(30f, surface.SurfaceHeightAt(new Vector2(-20f, 0f)));
        Assert.Equal(50f, surface.SurfaceHeightAt(new Vector2(200f, 0f)));
    }


    /**
     * ...and where the two kerb lines CROSS, which is the only place the two distances are
     * both zero while the two sides carry different heights.
     *
     * The ratio of the distances to the two chords is 0/0 there, and without the guard the
     * answer is a NaN that a vertex then carries into the mesh. Asserted as the exact height
     * the halfway blend gives - 0.4 of the way up one side and 0.3 up the other, so 35 -
     * because "it is finite" cannot tell a guess from a refusal.
     */
    [Fact]
    public void WhereTheTwoKerbsCrossTheSurfaceStillAnswersTheBlendOfThem()
    {
        var surface = RoadSurface.Of(
            Vector2.Zero, new Vector2(1f, 0f),
            new Vector2(10f, -1f), new Vector2(30f, 1f),
            new Vector2(90f, 1f), new Vector2(70f, -3f),
            0f, 100f);

        Assert.Equal(35f, surface.SurfaceHeightAt(new Vector2(42f, -0.2f)), 3);
    }


    /**
     * A stroke that is not in its own junction's angle array has no carriageway, and says
     * why.
     *
     * Unreachable from any generated city - which is exactly why it is a fixture. The
     * operator turns this into an ErrorThrow naming the junction; without the answer being
     * checkable at all, that branch is four corners of Vector2.Zero and a road built at the
     * cluster origin.
     */
    [Fact]
    public void AStrokeMissingFromItsOwnJunctionHasNoCarriageway()
    {
        var cd = StreetHarness.MakeCluster("cornersof", 1000f);

        var a = new StreetPoint() { ClusterId = 0 };
        a.SetPos(100f, -50f);
        var b = new StreetPoint() { ClusterId = 0 };
        var c = new StreetPoint() { ClusterId = 0 };
        var d = new StreetPoint() { ClusterId = 0 };

        var store = new StrokeStore(1000f);
        store.AddStroke(Stroke.CreateByAngleFrom(cd, a, b, 0f, 160f, true, 1.0f));
        store.AddStroke(Stroke.CreateByAngleFrom(cd, a, c, 90f, 160f, true, 1.0f));

        /*
         * Built from the same junction and never added to it, so a's angle array knows
         * nothing about it.
         */
        var orphan = Stroke.CreateByAngleFrom(cd, a, d, 45f, 160f, true, 1.0f);

        Assert.False(RoadSurface.TryCornersOf(
            orphan, out _, out _, out _, out _, out string why));
        Assert.Contains("street point A", why);
        Assert.Null(RoadSurface.OfStroke(orphan, new FlatStreetHeight(cd), Vector2.Zero));

        /*
         * ...and a stroke that IS in its junction has them.
         */
        Assert.True(RoadSurface.TryCornersOf(
            store.GetStrokes()[0], out _, out _, out _, out _, out _));
    }


    /**
     * Only the lanes that are on a carriageway carry one.
     *
     * The asymmetry is the point: a pavement is one kerb above the road and a crossing is
     * in the roadway, so a lane taking the carriageway's height where it should not have it
     * would be sunk into the very slab it is drawn on.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void OnlyCarLanesCarryACarriageway(string idString, float size)
    {
        var city = _city(idString, size, false);

        int nCar = 0, nFoot = 0;

        foreach (var nl in city.Content.Lanes)
        {
            if (nl.AllowedTypes.HasFlag(TransportationType.Car))
            {
                Assert.True(nl.Surface.HasValue,
                    $"{idString}/{size}: a car lane at {nl.Start.Position} is on no road");
                ++nCar;
            }
            else
            {
                Assert.Null(nl.Surface);
                ++nFoot;
            }
        }

        Assert.True(nCar > 50 && nFoot > 50,
            $"{idString}/{size}: {nCar} car and {nFoot} pedestrian lanes is too few");
    }


    /**
     * The blend across the road agrees with the emission's own answer at the two kerb
     * lines, where every road vertex is.
     *
     * Equality, so this cannot be satisfied by a blend that is merely close: the two are
     * the same expression at a lateral fraction of exactly 0 and exactly 1, and that is why
     * adding SurfaceHeightAt leaves the road mesh untouched.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void TheBlendAcrossTheRoadMeetsTheEmissionAtBothKerbs(string idString, float size)
    {
        var city = _city(idString, size, false);

        int nChecked = 0;

        foreach (var stroke in city.Strokes.GetStrokes())
        {
            Assert.True(RoadSurface.TryCornersOf(
                stroke, out var al, out var ar, out var bl, out var br, out _));

            var surface = RoadSurface.OfStroke(
                stroke, city.Cluster.StreetHeightSource, Vector2.Zero).Value;

            foreach (var corner in new[] { al, ar, bl, br })
            {
                Assert.Equal(surface.HeightAt(corner), surface.SurfaceHeightAt(corner));
                ++nChecked;
            }

            /*
             * ...and half way along each kerb, which is on a chord but not at a corner.
             */
            foreach (var mid in new[] { (al + bl) / 2f, (ar + br) / 2f })
            {
                Assert.True(
                    Single.Abs(surface.HeightAt(mid) - surface.SurfaceHeightAt(mid)) < 1e-3f,
                    $"{idString}/{size}: the two disagree by "
                    + $"{surface.HeightAt(mid) - surface.SurfaceHeightAt(mid)} half way "
                    + "along a kerb");
                ++nChecked;
            }
        }

        Assert.True(nChecked > 100, $"{idString}/{size}: {nChecked} corners is too few");
    }
}
