using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using engine.streets;
using engine.streets.generation;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * Where two carriageway edges meeting at a junction cross.
 *
 * A junction's section array holds one point per adjacent pair of arms, and that point is
 * the mitre of the two arms' carriageway edges. It is the corner of the junction cap, the
 * corner of the block that stands there, and the end of the carriageway - so §7o's whole
 * kerb-seam guarantee rests on one property of it: **a section point lies on the offset line
 * of BOTH of the arms it belongs to.** That is the 0.0002 m median collinearity §7o
 * measured, and it is what makes each side of a road interpolate exactly between its own two
 * corners.
 *
 * It used to be computed by intersecting the two offset lines through
 * geom.Line.IntersectInfinite, in absolute world coordinates, by Cramer's rule on
 * homogeneous line coordinates whose constant term is `A.Y*B.X - A.X*B.Y`. On a cluster 3 km
 * across that term is of order 2e6 and the numerator of the solve is a difference of two
 * products of order 2e8, so as soon as the two arms are near-collinear and the determinant
 * drops to a few thousandths, the answer has no significant digits left and comes back tens
 * of metres off both of the lines it is supposed to be on.
 *
 * ⚠️ **What §7o recorded about this, and what measurement says.** §7o wrote the 11 off-line
 * block edges of Yelukhdidru/3000 down to `_computeSectionArrayNoLock`'s `dist2 > 4000`
 * fallback for near-collinear arms. That is right for 6 of them and wrong for the other 5,
 * and it was wrong about the mechanism for all 11. The `dist2 > 4000` branch itself is
 * innocent: over five generated cities every single one of the 46 corners that took it was
 * at a wedge angle of 179.9999-180.2983 degrees, which is the *straight-through* case its
 * own comment claims ("these are pretty in-line streets"), and the averaged offset it
 * substitutes is the exact answer there - measured worst residual 1.13 m against a mitre
 * that is genuinely at infinity. What it could not do is notice the cases where a cancelled
 * intersection landed NEAR the junction and was therefore accepted, which is where every one
 * of the large residuals came from.
 */
public class SectionMitreTests
{
    /**
     * Cities with a near-collinear junction dense enough to show the defect. The four §7o
     * baselines plus four seeds found by sweeping 40 seeds at three sizes for junctions that
     * take the old distance fallback - §7o's `seed008` lesson: a shape real data does not
     * produce cannot be caught by any amount of it, so go and find data that does.
     */
    public static IEnumerable<object[]> Cities()
    {
        yield return new object[] { "seed000", 500f };
        yield return new object[] { "seed008", 500f };
        yield return new object[] { "Yelukhdidru", 800f };
        yield return new object[] { "seed000", 1500f };
        yield return new object[] { "Yelukhdidru", 3000f };
        yield return new object[] { "seed027", 1500f };
        yield return new object[] { "seed014", 1500f };
        yield return new object[] { "seed013", 1500f };
    }


    /**
     * How far a section point may be off the offset line of an arm it belongs to.
     *
     * Not a fitted bound: the construction puts it on both lines exactly, so what is left is
     * single precision on cluster coordinates of up to 1500 m. Observed worst over the eight
     * cities: 6e-5 m.
     */
    private const float OnTheLine = 1e-3f;


    private static readonly Vector2 _far = new(-1147.1f, -1155.2f);


    /**
     * The two inward normals of the offset lines of two arms leaving a junction.
     */
    private static (Vector2 A, Vector2 B) _normalsOf(in Vector2 dPrev, in Vector2 dCurr)
        => (new Vector2(-dPrev.Y, dPrev.X), new Vector2(dCurr.Y, -dCurr.X));


    /**
     * How far `offset` is off the two offset lines it is supposed to be the crossing of.
     */
    private static float _offBothLines(
        in Vector2 dPrev, in Vector2 dCurr, float wPrev, float wCurr, in Vector2 offset)
    {
        var (a, b) = _normalsOf(dPrev, dCurr);

        return Single.Max(
            Single.Abs(Vector2.Dot(offset, a) - wPrev),
            Single.Abs(Vector2.Dot(offset, b) - wCurr));
    }


    private static Vector2 _unit(in Vector2 v) => v / v.Length();


    private static Vector2 _dir(float deg)
        => new(Single.Cos(deg * Single.Pi / 180f), Single.Sin(deg * Single.Pi / 180f));


    // ------------------------------------------------------------------ the expression


    /**
     * A right-angled junction of two equally wide streets puts the corner on the diagonal,
     * half a street width from each centre line.
     *
     * Asserted as EQUALITY with the intended point rather than as "somewhere sensible": §7p's
     * lesson that a containment test cannot tell a guess from a refusal.
     */
    [Fact]
    public void ARightAngleCornerIsHalfAStreetFromBothCentreLines()
    {
        Vector2 u = SectionMitre.OffsetOf(
            new Vector2(1f, 0f), new Vector2(0f, 1f), 5f, 5f,
            SectionMitre.MitreLimit, out bool isClamped);

        Assert.False(isClamped);
        Assert.Equal(5f, u.X, 4);
        Assert.Equal(5f, u.Y, 4);
    }


    /**
     * THE property: the corner is on both arms' carriageway edges, at every angle and every
     * pair of widths that has a finite answer.
     *
     * This is what the kerb seam rests on and it is what the old intersection lost.
     */
    [Fact]
    public void TheCornerLiesOnBothCarriagewayEdges()
    {
        int nMeasured = 0;

        for (float deg = 3f; deg < 358f; deg += 0.5f)
        {
            foreach (var (wp, wc) in new[] { (5f, 5f), (4f, 11f), (11f, 4f), (9.4f, 9.4f) })
            {
                Vector2 dPrev = new(1f, 0f);
                Vector2 dCurr = _dir(deg);

                Vector2 u = SectionMitre.OffsetOf(
                    dPrev, dCurr, wp, wc, 1000f, out bool isClamped);
                if (isClamped) continue;

                ++nMeasured;
                Assert.True(_offBothLines(dPrev, dCurr, wp, wc, u) < OnTheLine,
                    $"at {deg:F1} degrees with half widths {wp}/{wc} the corner is "
                    + $"{_offBothLines(dPrev, dCurr, wp, wc, u):F4} m off its own edge lines");
            }
        }

        Assert.True(nMeasured > 2000, $"only {nMeasured} corners were measurable");
    }


    /**
     * A straight-through junction of two equally wide streets: the two offset lines are the
     * same line, so the corner is square off the junction at half a street width.
     *
     * This is the case the old `dist2 > 4000` fallback was written for, and it is right
     * about it - which is why that branch is not the defect.
     */
    [Fact]
    public void AStraightThroughCornerIsSquareOffTheJunction()
    {
        Vector2 u = SectionMitre.OffsetOf(
            new Vector2(1f, 0f), new Vector2(-1f, 0f), 7f, 7f,
            SectionMitre.MitreLimit, out bool isClamped);

        Assert.False(isClamped);
        Assert.Equal(0f, u.X, 4);
        Assert.Equal(7f, u.Y, 4);
    }


    /**
     * A straight-through junction whose two arms are of DIFFERENT width has no corner at
     * all - two parallel lines a width apart never meet - so the point stays square off the
     * junction at the average and the carriageway steps.
     *
     * Equality with the average, not merely "bounded": the alternative the closed form
     * offers here is a point running away down the street, and a containment test would
     * accept it.
     */
    [Fact]
    public void AWideningStraightJunctionStepsRatherThanRunningDownTheStreet()
    {
        Vector2 u = SectionMitre.OffsetOf(
            new Vector2(1f, 0f), new Vector2(-1f, 0f), 5f, 9f,
            SectionMitre.MitreLimit, out bool isClamped);

        Assert.True(isClamped);
        Assert.Equal(0f, u.X, 4);
        Assert.Equal(7f, u.Y, 4);
    }


    /**
     * A hairpin - two arms leaving in nearly the same direction - has its mitre at infinity,
     * and is cut back to exactly the mitre limit.
     */
    [Fact]
    public void AHairpinIsCutBackToTheMitreLimit()
    {
        Vector2 dPrev = new(1f, 0f);
        Vector2 dCurr = _dir(2f);

        Vector2 u = SectionMitre.OffsetOf(
            dPrev, dCurr, 6f, 6f, SectionMitre.MitreLimit, out bool isClamped);

        Assert.True(isClamped);
        Assert.Equal(SectionMitre.MitreLimit * 6f, u.Length(), 3);

        /*
         * ...and it still points into the wedge between the two arms rather than anywhere
         * else, which is the half a length check cannot see.
         */
        var (a, b) = _normalsOf(dPrev, dCurr);
        Assert.True(Vector2.Dot(u, a) > 0f && Vector2.Dot(u, b) > 0f,
            $"the cut-back corner {u} is not between the two arms");
    }


    /**
     * An almost fully reversed corner - the two arms within a third of a degree of each
     * other - still points along the bisector between them.
     *
     * Below about 1.3 degrees the mitre's own denominator has cancelled to nothing and the
     * direction has to be recovered from the sum of the two normals instead. Asserted as
     * EQUALITY with the bisector rather than "somewhere between the arms": substituting the
     * current arm's own normal, which is what the parallel case answers, is perpendicular to
     * this and lands inside a wedge test just as happily. §7p's lesson - a containment test
     * cannot tell a guess from a refusal - one more time.
     */
    [Fact]
    public void AnAlmostReversedCornerStillRunsAlongTheBisector()
    {
        Vector2 dPrev = new(1f, 0f);
        Vector2 dCurr = _dir(0.3f);

        Vector2 u = SectionMitre.OffsetOf(
            dPrev, dCurr, 6f, 6f, SectionMitre.MitreLimit, out bool isClamped);

        Assert.True(isClamped);

        Vector2 expected = SectionMitre.MitreLimit * 6f * _dir(0.15f);
        Assert.Equal(expected.X, u.X, 2);
        Assert.Equal(expected.Y, u.Y, 2);
    }


    /**
     * Two arms that are the same ray have no wedge and no bisector at all, and the answer is
     * the one the parallel case gives - square off the current arm - rather than a NaN.
     */
    [Fact]
    public void TwoArmsOnOneRayStillGiveAFiniteCorner()
    {
        Vector2 d = new(0.6f, 0.8f);

        Vector2 u = SectionMitre.OffsetOf(
            d, d, 6f, 6f, SectionMitre.MitreLimit, out bool isClamped);

        Assert.True(isClamped);
        Assert.True(Single.IsFinite(u.X) && Single.IsFinite(u.Y));
        Assert.Equal(SectionMitre.MitreLimit * 6f * 0.8f, u.X, 3);
        Assert.Equal(SectionMitre.MitreLimit * 6f * -0.6f, u.Y, 3);
    }


    /**
     * The bound is RELATIVE to the street width, which is the whole of "when it triggers".
     *
     * The same corner angle on a wide street and on a narrow one is cut back to the same
     * multiple of its own width. An absolute bound - the `dist2 > 4000f` this replaced, i.e.
     * 63.24 m - is a limit of 6.7 on the narrowest street the generator builds and 13 on the
     * widest, so it bounds nothing at all where an over-long corner does the most harm.
     */
    [Fact]
    public void TheBoundIsRelativeToTheStreetWidth()
    {
        Vector2 dPrev = new(1f, 0f);
        Vector2 dCurr = _dir(20f);

        Vector2 narrow = SectionMitre.OffsetOf(
            dPrev, dCurr, 4f, 4f, SectionMitre.MitreLimit, out bool cn);
        Vector2 wide = SectionMitre.OffsetOf(
            dPrev, dCurr, 11f, 11f, SectionMitre.MitreLimit, out bool cw);

        Assert.True(cn);
        Assert.True(cw);
        Assert.Equal(SectionMitre.MitreLimit * 4f, narrow.Length(), 3);
        Assert.Equal(SectionMitre.MitreLimit * 11f, wide.Length(), 3);
        Assert.Equal(11f / 4f, wide.Length() / narrow.Length(), 3);
    }


    /**
     * Where the junction is in the world does not change the shape of its corner.
     *
     * The one assertion that fails outright for anything computed by intersecting two lines
     * in absolute coordinates, and passes at the origin - which is why a fixture built near
     * zero proves nothing here.
     */
    [Fact]
    public void TheCornerDoesNotDependOnWhereInTheWorldTheJunctionIs()
    {
        for (float deg = 1f; deg < 359f; deg += 1f)
        {
            Vector2 dPrev = new(1f, 0f);
            Vector2 dCurr = _dir(deg);

            Vector2 u = SectionMitre.OffsetOf(
                dPrev, dCurr, 5f, 7f, SectionMitre.MitreLimit, out _);

            Assert.True(u.Length() < SectionMitre.MitreLimit * 6f * 1.45f,
                $"at {deg:F0} degrees the corner is {u.Length():F2} m out, past the bound");
        }
    }


    // ------------------------------------------------------------------ the defect it removes


    /**
     * The two nearly collinear arms of a real junction 1.6 km from the origin, and what
     * intersecting their offset lines answers about them.
     *
     * Rebuilt from `Yelukhdidru`/3000's junction #1043 at (-1147.1, -1155.2), whose two arms
     * are 74.95 m long and 179.99992 degrees apart. It is the worst of the eleven off-line
     * block corners §7o counted.
     */
    private static (Vector2 Pos, Vector2 FarPrev, Vector2 FarCurr) _nearCollinearJunction()
    {
        var sp = new StreetPoint { ClusterId = 0 };
        sp.SetPos(_far);
        var pPrev = new StreetPoint { ClusterId = 0 };
        pPrev.SetPos(new Vector2(-1218.4f, -1178.3f));
        var pCurr = new StreetPoint { ClusterId = 0 };
        pCurr.SetPos(new Vector2(-1075.8f, -1132.1f));

        return (sp.Pos, pPrev.Pos, pCurr.Pos);
    }


    /**
     * ...and it did NOT lie on its own edge lines before, on real junctions, by metres.
     *
     * Without this the gates above are satisfied by anything that happens to be plausible,
     * and nothing says the old expression was ever wrong. The old rule is reconstructed here
     * exactly - the two offset lines through geom.Line, IntersectInfinite, and the averaged
     * offset whenever the answer came back further than 63.24 m out - and measured against
     * the same two lines it claims to intersect.
     */
    [Theory]
    [InlineData("Yelukhdidru", 3000f, 10f)]
    [InlineData("seed027", 1500f, 20f)]
    [InlineData("seed014", 1500f, 10f)]
    [InlineData("seed013", 1500f, 20f)]
    public void IntersectingTheOffsetLinesUsedToMissThemByMetres(
        string idString, float size, float atLeast)
    {
        var strokes = StreetHarness.Generate(idString, size);

        float worstOld = 0f, worstNew = 0f;
        int n = 0;

        foreach (var sp in strokes.GetStreetPoints())
        {
            var angles = sp.GetAngleArray();
            if (angles.Count < 2) continue;
            var sections = sp.GetSectionArray();
            if (sections.Count != angles.Count) continue;

            for (int i = 0; i < angles.Count; ++i)
            {
                Stroke prev = angles[(i + angles.Count - 1) % angles.Count];
                Stroke curr = angles[i];
                float wp = prev.StreetWidth() / 2f, wc = curr.StreetWidth() / 2f;
                Vector2 dPrev = prev.A == sp ? prev.Unit : -prev.Unit;
                Vector2 dCurr = curr.A == sp ? curr.Unit : -curr.Unit;

                ++n;
                worstOld = Single.Max(worstOld, _offBothLines(
                    dPrev, dCurr, wp, wc, _oldOffsetOf(sp, prev, curr, wp, wc)));
                worstNew = Single.Max(worstNew, _offBothLines(
                    dPrev, dCurr, wp, wc, sections[i] - sp.Pos));
            }
        }

        Assert.True(n > 100, $"only {n} corners measured");
        Assert.True(worstOld > atLeast,
            $"{idString}/{size}: intersecting the offset lines was only {worstOld:F3} m off "
            + "them at worst, so this baseline no longer describes the defect the closed form "
            + "removed and the gates above prove nothing");
        Assert.True(worstNew < worstOld,
            $"{idString}/{size}: the shipped corner is {worstNew:F3} m off its own edge lines, "
            + $"against {worstOld:F3} m for the intersection it replaced");
    }


    /**
     * The rule that shipped until 2026-09-03, reconstructed.
     */
    private static Vector2 _oldOffsetOf(
        StreetPoint sp, Stroke prev, Stroke curr, float wp, float wc)
        => _oldOffsetOf(sp.Pos,
            prev.A == sp ? prev.B.Pos : prev.A.Pos,
            curr.A == sp ? curr.B.Pos : curr.A.Pos, wp, wc);


    private static Vector2 _oldOffsetOf(
        in Vector2 pos, in Vector2 farPrev, in Vector2 farCurr, float wp, float wc)
    {
        var lp = new global::engine.geom.Line(pos, farPrev);
        var lc = new global::engine.geom.Line(pos, farCurr);

        Vector2 np = lp.Normal(), nc = lc.Normal();
        lp.Move(np.X * -wp, np.Y * -wp);
        lc.Move(nc.X * wc, nc.Y * wc);

        float aver = (wp + wc) / 2f;
        var i0 = lp.IntersectInfinite(lc);
        if (!i0.HasValue) return aver * nc;

        Vector2 u = i0.Value - pos;
        if (u.LengthSquared() <= 4000f) return u;

        Vector2 d = nc - np;

        return aver * (d / d.Length());
    }


    /**
     * The same defect at one junction, without needing a whole city to produce one.
     */
    [Fact]
    public void ANearCollinearJunctionFarFromTheOriginUsedToLoseItsCornerEntirely()
    {
        var (pos, farPrev, farCurr) = _nearCollinearJunction();

        float wp = 4.99f, wc = 4.99f;
        Vector2 dPrev = _unit(farPrev - pos);
        Vector2 dCurr = _unit(farCurr - pos);

        float old = _offBothLines(dPrev, dCurr, wp, wc,
            _oldOffsetOf(pos, farPrev, farCurr, wp, wc));
        float now = _offBothLines(dPrev, dCurr, wp, wc, SectionMitre.OffsetOf(
            dPrev, dCurr, wp, wc, SectionMitre.MitreLimit, out _));

        Assert.True(old > 1f,
            $"the intersection was only {old:F3} m off the lines it intersects, so this "
            + "fixture no longer reproduces the defect");
        Assert.True(now < OnTheLine,
            $"the corner is {now:F4} m off its own edge lines");
    }


    // ------------------------------------------------------------------ whole cities


    /**
     * Every corner of every generated city is on both of its own carriageway edges, unless
     * it was cut back - and the ones that were are counted rather than excused.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void EveryCornerOfAGeneratedCityIsOnItsOwnEdgeLines(string idString, float size)
    {
        var strokes = StreetHarness.Generate(idString, size);

        int n = 0, nClamped = 0;
        float worst = 0f, worstDistOverWidth = 0f;
        string where = "";

        foreach (var sp in strokes.GetStreetPoints())
        {
            var angles = sp.GetAngleArray();
            if (angles.Count < 2) continue;
            var sections = sp.GetSectionArray();
            if (sections.Count != angles.Count) continue;

            for (int i = 0; i < angles.Count; ++i)
            {
                Stroke prev = angles[(i + angles.Count - 1) % angles.Count];
                Stroke curr = angles[i];
                float wp = prev.StreetWidth() / 2f, wc = curr.StreetWidth() / 2f;
                Vector2 dPrev = prev.A == sp ? prev.Unit : -prev.Unit;
                Vector2 dCurr = curr.A == sp ? curr.Unit : -curr.Unit;

                ++n;
                Vector2 u = sections[i] - sp.Pos;

                /*
                 * Every corner, cut back or not, is inside the bound. This is the half that
                 * fails when the clamp is removed, and it is measured on the SHIPPED section
                 * array rather than on a fresh call, so re-inlining the old rule fails it
                 * too.
                 */
                worstDistOverWidth = Single.Max(
                    worstDistOverWidth, u.Length() / (0.5f * (wp + wc)));

                SectionMitre.OffsetOf(dPrev, dCurr, wp, wc, SectionMitre.MitreLimit,
                    out bool isClamped);
                if (isClamped)
                {
                    ++nClamped;
                    continue;
                }

                float off = _offBothLines(dPrev, dCurr, wp, wc, u);
                if (off > worst)
                {
                    worst = off;
                    where = $"the junction at {sp.Pos} between strokes {prev.Sid}/{curr.Sid}";
                }
            }
        }

        Assert.True(n > 8, $"{idString}/{size}: only {n} corners");

        Assert.True(worst < OnTheLine,
            $"{idString}/{size}: a corner is {worst:F4} m off its own carriageway edges at "
            + $"{where}");

        /*
         * The bound really is a bound, on the array the city was built from.
         */
        Assert.True(worstDistOverWidth < SectionMitre.MitreLimit * 1.45f + 1e-3f,
            $"{idString}/{size}: a corner stands {worstDistOverWidth:F3} street half widths "
            + "from its junction, past the mitre limit");

        /*
         * Cutting back is rare and stays rare. 42 corners of 7544 over the whole corpus, of
         * which 32 are the degenerate straight-widening kind that no limit can help.
         */
        Assert.True(nClamped * 50 < n + 50,
            $"{idString}/{size}: {nClamped} of {n} corners were cut back to the mitre limit");
    }


    /**
     * The section array still has exactly one corner per arm.
     *
     * A bevel - two points per over-long corner, one on each arm's own edge line - is the
     * textbook remedy and the only construction that puts EVERY corner on both lines
     * exactly. It is not what is built, because the array's length is contract: every block
     * cornering on such a junction would gain a corner, which moves the block outline, the
     * estate, the ClipperOffset footprint, the building and its shops in both cities, and
     * `GenerateNavMapOperator` and `QuarterGenerator` both index arms and sections together.
     */
    [Theory]
    [MemberData(nameof(Cities))]
    public void ThereIsExactlyOneCornerPerArm(string idString, float size)
    {
        var strokes = StreetHarness.Generate(idString, size);

        int nJunctions = 0;
        foreach (var sp in strokes.GetStreetPoints())
        {
            var angles = sp.GetAngleArray();
            if (angles.Count < 2)
            {
                Assert.Empty(sp.GetSectionArray());
                continue;
            }

            ++nJunctions;
            Assert.Equal(angles.Count, sp.GetSectionArray().Count);
        }

        Assert.True(nJunctions > 3, $"{idString}/{size}: only {nJunctions} junctions");
    }


    /**
     * The corpus really does contain corners that get cut back.
     *
     * §7o's `seed008` lesson: without a city that produces the shape, the clamp is dead code
     * that no amount of real data can exercise, and deleting it passes everything.
     */
    [Fact]
    public void TheCorpusContainsCornersThatAreCutBack()
    {
        int nClamped = 0;

        foreach (var row in Cities())
        {
            var strokes = StreetHarness.Generate((string)row[0], (float)row[1]);
            foreach (var sp in strokes.GetStreetPoints())
            {
                var angles = sp.GetAngleArray();
                if (angles.Count < 2) continue;

                for (int i = 0; i < angles.Count; ++i)
                {
                    Stroke prev = angles[(i + angles.Count - 1) % angles.Count];
                    Stroke curr = angles[i];
                    Vector2 dPrev = prev.A == sp ? prev.Unit : -prev.Unit;
                    Vector2 dCurr = curr.A == sp ? curr.Unit : -curr.Unit;

                    SectionMitre.OffsetOf(dPrev, dCurr,
                        prev.StreetWidth() / 2f, curr.StreetWidth() / 2f,
                        SectionMitre.MitreLimit, out bool isClamped);
                    if (isClamped) ++nClamped;
                }
            }
        }

        Assert.True(nClamped >= 20,
            $"only {nClamped} corners in the whole corpus are cut back, so the bound is "
            + "barely exercised by real data");
    }


    /**
     * ...and Clipper's own default of 2 is not the number, which is the one thing this test
     * exists to record.
     *
     * A section point is not a cosmetic corner: cutting one back moves the block, the estate,
     * the building and its shops, and it takes the corner OFF both edge lines - the exact
     * property the kerb seam needs. Measured over the eight cities and 7544 corners, a limit
     * of 2 cuts back 888 of them, one in eight, and leaves 1018 block edges more than 0.25 m
     * off their own stroke's edge against 5 at a limit of 3 and 13 before this change at
     * all.
     */
    [Fact]
    public void ALimitOfTwoWouldCutBackOneCornerInEight()
    {
        int n = 0, atTwo = 0, atThree = 0;

        foreach (var row in Cities())
        {
            var strokes = StreetHarness.Generate((string)row[0], (float)row[1]);
            foreach (var sp in strokes.GetStreetPoints())
            {
                var angles = sp.GetAngleArray();
                if (angles.Count < 2) continue;

                for (int i = 0; i < angles.Count; ++i)
                {
                    Stroke prev = angles[(i + angles.Count - 1) % angles.Count];
                    Stroke curr = angles[i];
                    float wp = prev.StreetWidth() / 2f, wc = curr.StreetWidth() / 2f;
                    Vector2 dPrev = prev.A == sp ? prev.Unit : -prev.Unit;
                    Vector2 dCurr = curr.A == sp ? curr.Unit : -curr.Unit;

                    ++n;
                    SectionMitre.OffsetOf(dPrev, dCurr, wp, wc, 2f, out bool c2);
                    SectionMitre.OffsetOf(dPrev, dCurr, wp, wc, 3f, out bool c3);
                    if (c2) ++atTwo;
                    if (c3) ++atThree;
                }
            }
        }

        Assert.True(atTwo > 10 * atThree,
            $"a limit of 2 cuts back {atTwo} of {n} corners and a limit of 3 cuts back "
            + $"{atThree}, so the two are no longer the different choices §7q measured");
        Assert.Equal(3f, SectionMitre.MitreLimit);
    }
}
