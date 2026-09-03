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
        bool right = _isRight(p);
        return HeightA + _fractionAt(p, right) * (HeightB - HeightA);
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
