using System;
using System.Numerics;

namespace engine.streets.generation;


/**
 * Where the road's own surface is: at a junction, and along the two lines where a
 * carriageway meets the kerbs of the blocks beside it.
 *
 * **There is no sidewalk object.** GenerateClusterQuartersOperator extrudes a city block's
 * outline up by MetaGen.QuarterSidewalkOffset, so the top face is the pavement and the
 * SIDES are the kerb - which means the block's outline IS the line where the kerb meets the
 * carriageway. That line is emitted twice, by two operators, from two expressions, and
 * nothing used to say they agreed.
 *
 * They do agree in plan, and that was measured before anything was changed: a block corner
 * is a section point of its own junction, and the two section points bounding one stroke at
 * its two ends both lie on the same offset of that stroke's centre line, so the block edge
 * between them is collinear with the carriageway's edge to within the 0.1 m the junction
 * positions are quantised to (median 0.0001 m over 2936 corners, p99 0.004 m). What they
 * disagreed about was HEIGHT, and only on a slope.
 *
 * The kerb is a straight chord between two corners, each at its own junction's surface
 * height. The carriageway used to be sheared onto its slope by ONE window along the stroke's
 * centre line - flat at hA up to the further of the two section points at A, climbing, then
 * flat at hB - which agrees with the chord at both ends and nowhere in between. Measured on
 * the shipped terrain over the four baseline cities, the kerb's underside stood clear of the
 * road, or sank into it, by more than the 0.15 m kerb itself at 27-30 % of the sampled
 * positions, by more than half a metre at 11 %, and by up to 6.5 m. In the FLAT city the
 * same measurement is exactly 0.000 m at every percentile, which is why this was never seen
 * before the terrain-following city became the default.
 *
 * The fix is that each SIDE of a stroke owns its own interpolation, between the two section
 * points that side's kerb actually runs between. That is exact rather than close:
 *
 *   - at either end the chord parameter is 0 or 1, so the corner carries its junction's own
 *     height - which is what lets the flat junction cap and the two strokes meeting there
 *     still join without a tear, the property the single window existed to protect;
 *   - in between, the axial coordinate along the stroke is an AFFINE function of position
 *     along the chord, so interpolating in one is interpolating in the other, and the
 *     carriageway's edge is the same straight segment in space as the kerb's underside.
 *
 * A straight junction has both section points at the same axial distance, so both sides
 * share one window and this reduces to exactly what was emitted before - which is why every
 * ramp OverpassBuilder builds is unchanged float for float.
 */
public readonly struct RoadSurface
{
    /**
     * Below this a chord has no length to interpolate along, and the two ends simply take
     * their own heights.
     */
    public const float MinSpan = 0.001f;


    /**
     * World height of the road at one junction.
     *
     * A junction is one node of the stroke graph, so it has exactly ONE, and the junction
     * cap's fan, the two strokes' surfaces, the cap's collider, the deck collider and the
     * kerb of every block cornering there all read this same number. That is the whole
     * mechanism by which a non-planar network holds together at its seams, and it used to be
     * written out five times - twice under the name ClusterStreetHeight and three times
     * under CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE, two constants that are both 2.0 and are
     * not the same constant. Two of the five also dropped the deck term.
     *
     * Here rather than at the call sites because those call sites are inside fragment
     * operators, where nothing can check them: reading the city AVERAGE instead of the
     * junction's own relaxed height compiles, keeps a flat city bit for bit identical, and
     * puts a pancake at the mean height over every junction of a terrain-following one. It
     * is not caught by ClusterGroundHeightTests either, since those operators are already on
     * that allow list for the flat floor plane.
     */
    public static float HeightAtJunction(IStreetHeightSource heightSource, in StreetPoint sp)
    {
        return heightSource.GroundHeightAt(sp)
               + world.MetaGen.CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE
               + sp.LevelElevation;
    }


    /**
     * The four corners bounding one stroke's carriageway, in the same plan space the
     * stroke's own street points are in.
     *
     * Here rather than inline in GenerateClusterStreetsOperator._generateStreetRun, which
     * is where it was written and is still its only geometry consumer, because the SATNAV
     * guideline has to be drawn on the very same surface. A ribbon built from a second
     * derivation of "where does this carriageway begin and end" is a ribbon that agrees
     * with the road until one of the two is edited - and the ribbon is drawn over the road,
     * where a disagreement of a decimetre is the whole visible defect.
     *
     * Left and right are from A's point of view. A stroke that is the only arm at one of
     * its junctions has no section array to read there, so that end is taken from the
     * stroke's own normal, exactly as the emission does.
     *
     * @param why
     *     Why there are no corners, for the caller to report. The two failures are a
     *     malformed junction - a stroke missing from its own angle array, or a section
     *     array that does not match it - and neither is recoverable here.
     */
    public static bool TryCornersOf(
        in streets.Stroke stroke,
        out Vector2 leftA, out Vector2 rightA, out Vector2 leftB, out Vector2 rightB,
        out string why)
    {
        leftA = rightA = leftB = rightB = Vector2.Zero;
        why = null;

        float hsw = stroke.StreetWidth() / 2f;
        Vector2 n = stroke.Normal;

        if (!_endOf(stroke, stroke.A, hsw, n, true, out leftA, out rightA, out why)) return false;
        if (!_endOf(stroke, stroke.B, hsw, n, false, out leftB, out rightB, out why)) return false;

        return true;
    }


    /**
     * One end of a stroke's carriageway.
     *
     * The angle array is sorted by outgoing angle, so at A the entry AT this stroke's index
     * is the intersection with the previous arm and the NEXT entry is the intersection with
     * the next arm. At B the stroke is incoming, so the same two entries name the opposite
     * sides - which is why the two ends read the array the other way round and why that is
     * not a copy-paste slip.
     */
    private static bool _endOf(
        in streets.Stroke stroke, in streets.StreetPoint sp,
        float hsw, in Vector2 n, bool isA,
        out Vector2 left, out Vector2 right, out string why)
    {
        left = right = Vector2.Zero;
        why = null;

        var angArr = sp.GetAngleArray();
        if (angArr.Count <= 1)
        {
            left = sp.Pos - n * hsw;
            right = sp.Pos + n * hsw;
            return true;
        }

        int idx = angArr.IndexOf(stroke);
        if (idx < 0)
        {
            why = $"stroke is not in street point {(isA ? "A" : "B")}.";
            return false;
        }

        var secArr = sp.GetSectionArray();
        if (secArr.Count != angArr.Count)
        {
            why = $"for point {(isA ? "a" : "b")}: Section array and angle array differ in "
                  + $"size: {secArr.Count} != {angArr.Count}.";
            return false;
        }

        int idxNext = (idx + 1) % angArr.Count;

        if (isA)
        {
            left = secArr[idxNext];
            right = secArr[idx];
        }
        else
        {
            left = secArr[idx];
            right = secArr[idxNext];
        }

        return true;
    }


    /**
     * The surface of one stroke's carriageway, in world plan coordinates.
     *
     * The one entry point for anything that is not the emission itself: the emission has
     * its four corners in hand already and builds the surface from those, and everything
     * else - the satnav guideline, and whatever comes after it - gets the same surface from
     * the same corners rather than deriving a second one.
     *
     * @param v2Offset
     *     Where the cluster's origin is, so that the surface answers in the same space the
     *     caller's positions are in. NavJunction is world space; the emission is fragment
     *     relative.
     * @returns null when the junctions at either end are malformed, in which case there is
     *     no carriageway to be on either.
     */
    public static RoadSurface? OfStroke(
        in streets.Stroke stroke, streets.IStreetHeightSource heightSource, in Vector2 v2Offset)
    {
        if (!TryCornersOf(stroke, out var al, out var ar, out var bl, out var br, out _))
        {
            return null;
        }

        return Of(
            stroke.A.Pos + v2Offset, stroke.Unit,
            al + v2Offset, ar + v2Offset, bl + v2Offset, br + v2Offset,
            HeightAtJunction(heightSource, stroke.A),
            HeightAtJunction(heightSource, stroke.B));
    }


    public readonly float HeightA;
    public readonly float HeightB;

    private readonly Vector2 _origin;
    private readonly Vector2 _unit;

    private readonly Vector2 _leftA, _leftB, _rightA, _rightB;
    private readonly float _dLeftA, _dLeftB, _dRightA, _dRightB;


    /**
     * True when the whole surface is at one height, so nothing needs shearing at all.
     */
    public bool IsLevel => HeightA == HeightB;


    /**
     * @param origin, unit
     *     Start of the stroke's centre line and the unit vector along it, in the same plan
     *     space as the four corners and as anything later asked about.
     * @param leftA, rightA
     *     The two section points bounding this stroke at its A junction: al and ar in
     *     GenerateClusterStreetsOperator._generateStreetRun's own naming, i.e. adjacent
     *     entries of that junction's section array. Each is the corner of the block on that
     *     side, so a block edge runs leftA to leftB or rightA to rightB and never mixes
     *     the two.
     * @param heightA, heightB
     *     HeightAtJunction of the stroke's two junctions.
     */
    public static RoadSurface Of(
        in Vector2 origin, in Vector2 unit,
        in Vector2 leftA, in Vector2 rightA, in Vector2 leftB, in Vector2 rightB,
        float heightA, float heightB)
    {
        return new RoadSurface(origin, unit, leftA, rightA, leftB, rightB, heightA, heightB);
    }


    private RoadSurface(
        in Vector2 origin, in Vector2 unit,
        in Vector2 leftA, in Vector2 rightA, in Vector2 leftB, in Vector2 rightB,
        float heightA, float heightB)
    {
        _origin = origin;
        _unit = unit;
        _leftA = leftA;
        _leftB = leftB;
        _rightA = rightA;
        _rightB = rightB;

        _dLeftA = Vector2.Dot(leftA - origin, unit);
        _dLeftB = Vector2.Dot(leftB - origin, unit);
        _dRightA = Vector2.Dot(rightA - origin, unit);
        _dRightB = Vector2.Dot(rightB - origin, unit);

        HeightA = heightA;
        HeightB = heightB;
    }


    /**
     * Which of the two kerb lines this plan position belongs to.
     *
     * By distance to the chord rather than by the sign of the offset from the centre line,
     * because the two are not the same thing everywhere: StreetPoint's section array falls
     * back to an averaged offset when two arms are nearly collinear and their offset lines
     * intersect more than 63 m out, and such a section point can land on the far side. It is
     * rare - 7 of 2477 block edges in the largest baseline - but a corner assigned to the
     * opposite chord would take a height that is not its junction's, which is precisely the
     * defect being removed. A point ON a chord is at distance zero from it and cannot be
     * misassigned.
     */
    private bool _isRight(in Vector2 p)
    {
        return _distanceToChord(p, _rightA, _rightB) < _distanceToChord(p, _leftA, _leftB);
    }


    private static float _distanceToChord(in Vector2 p, in Vector2 a, in Vector2 b)
    {
        Vector2 ab = b - a;
        float l = ab.Length();
        if (l < MinSpan)
        {
            return (p - a).Length();
        }

        return Single.Abs((p.X - a.X) * ab.Y - (p.Y - a.Y) * ab.X) / l;
    }


    /**
     * The road surface's height at a plan position on or beside this stroke.
     */
    public float HeightAt(in Vector2 p)
    {
        return _heightOn(p, _isRight(p));
    }


    /**
     * One side's own height at a position's axial coordinate.
     *
     * Through Single.Lerp rather than HeightA + f * (HeightB - HeightA), because the two
     * differ by a unit in the last place at f = 1 - the sum of a height and a difference of
     * two heights is not the second height - and f is EXACTLY 1 at every corner of the B
     * end and everywhere over its cap, which is where the whole network is supposed to meet
     * at one number. Lerp is defined as a*(1-t) + b*t, so it returns HeightA and HeightB
     * themselves at the two ends.
     */
    private float _heightOn(in Vector2 p, bool right)
    {
        return Single.Lerp(HeightA, HeightB, _fractionAt(p, right));
    }


    /**
     * Where across the carriageway a plan position is: 0 on the left kerb line, 1 on the
     * right, clamped outside them.
     *
     * By the ratio of the distances to the two chords rather than by an offset from the
     * centre line, for the reason _isRight is: the chords ARE the two lines the surface is
     * emitted between, and a point on one of them is at distance zero from it whatever the
     * section array did. That also makes this exactly 0 or exactly 1 at every vertex the
     * road emits, since every one of them lies on a kerb line - which is what lets
     * SurfaceHeightAt below agree with HeightAt there rather than merely come close.
     */
    private float _lateralFractionAt(in Vector2 p)
    {
        float dl = _distanceToChord(p, _leftA, _leftB);
        float dr = _distanceToChord(p, _rightA, _rightB);
        float sum = dl + dr;

        return sum < MinSpan ? 0.5f : Single.Clamp(dl / sum, 0f, 1f);
    }


    /**
     * The height of the emitted carriageway anywhere ACROSS its width, not only on one of
     * its two kerb lines.
     *
     * HeightAt answers the question the shear asks - every vertex the road emits sits on
     * one of the two kerb lines, at exactly plus or minus half the street width off the
     * centre - so it picks a side and is exact there. Anything drawn ON the road instead
     * lies between the two, and the two are not at the same height: each side climbs
     * between its own pair of section points, so at a bend the carriageway carries a real
     * cross fall, measured over the four baseline cities on the shipped terrain at 0.10 to
     * 0.15 m between the two kerbs at the median, 1.0 to 1.2 m at p95 and up to 3.6 m.
     *
     * So this is the linear blend between the two sides at the position's own lateral
     * fraction, which is what the emitted quads interpolate between their two kerb rows.
     * It agrees with HeightAt at the kerbs identically, and NOTHING in the emission calls
     * it - the road mesh is untouched by its existence.
     */
    public float SurfaceHeightAt(in Vector2 p)
    {
        float hLeft = _heightOn(p, false);
        float hRight = _heightOn(p, true);

        /*
         * The three cases where the answer is one of the two sides and not a mixture, taken
         * before the blend rather than through it: a * (1 - u) + a * u is a rounding away
         * from a, and this is asked at the ends of a ribbon, where the road, the junction
         * cap and everything cornering there are supposed to meet at ONE number rather than
         * at two that differ in the last place.
         */
        if (hLeft == hRight) return hLeft;

        float u = _lateralFractionAt(p);
        if (u <= 0f) return hLeft;
        if (u >= 1f) return hRight;

        return Single.Lerp(hLeft, hRight, u);
    }


    /**
     * How far along the stroke a plan position is, in metres from the A junction.
     */
    public float AxialAt(in Vector2 p) => Vector2.Dot(p - _origin, _unit);


    /**
     * The axial distances at which this surface stops being one plane: the four section
     * points, two per side, each of which is where that side's kerb stops being flat at its
     * junction's height and starts climbing.
     *
     * A ribbon drawn along this stroke is piecewise affine with exactly these breaks in it,
     * and straight between them - so subdividing there is exact rather than merely finer.
     */
    public void BreakpointsInto(Span<float> four)
    {
        four[0] = _dLeftA;
        four[1] = _dRightA;
        four[2] = _dLeftB;
        four[3] = _dRightB;
    }


    /**
     * The surface normal at a plan position: straight up rotated back by that side's own
     * slope, in the vertical plane the stroke runs along. A climbing surface with a
     * straight-up normal lights as though it were flat.
     */
    public Vector3 NormalAt(in Vector2 p)
    {
        float slope = SlopeOn(_isRight(p));
        return Vector3.Normalize(new Vector3(-slope * _unit.X, 1f, -slope * _unit.Y));
    }


    /**
     * Rise over run of one side, over the run that actually climbs.
     */
    public float SlopeOn(bool right)
    {
        float span = right ? _dRightB - _dRightA : _dLeftB - _dLeftA;
        return Single.Abs(span) < MinSpan ? 0f : (HeightB - HeightA) / span;
    }


    private float _fractionAt(in Vector2 p, bool right)
    {
        float d = Vector2.Dot(p - _origin, _unit);
        float dA = right ? _dRightA : _dLeftA;
        float dB = right ? _dRightB : _dLeftB;
        float span = dB - dA;

        if (Single.Abs(span) < MinSpan)
        {
            return d <= dA ? 0f : 1f;
        }

        return Single.Clamp((d - dA) / span, 0f, 1f);
    }
}
