using System.Numerics;

namespace builtin.tools;


/**
 * The two inset points that belong to ONE edge of an extrusion's cap.
 *
 * A cap with an inset is a rim of quads along the polygon's edges plus the interior left
 * over, and a rim quad is level across its width exactly when every one of its vertices
 * carries the height the outer edge has at that vertex's own projection onto it. Since
 * both inset points here belong to a single edge, that condition is met by construction
 * and the quad's four heights lie on one plane whose gradient runs purely along the edge.
 *
 * The points belong to the edge and NOT to the corners at its ends, which is the whole
 * design. A single inset point shared by two edges - the mitre, the obvious construction -
 * has to carry two different heights to keep both of its neighbours level, and the two
 * differ by roughly twice the pavement width times the slope. Giving it either one cracks
 * the surface; giving it the corner's own height leaves a cross-fall of the along-edge
 * slope times cot(angle/2), which at the median block corner of 90 degrees is the
 * along-edge slope again, i.e. no improvement at all. Measured over real generated cities,
 * that construction moved the median cross-fall from 7.2 % to 6.7 %.
 *
 * So the edges do not share. Each edge's inset runs from Start to End, both offset the
 * full width, and the two neighbouring edges meet only at the outer corner itself - where
 * both agree on the corner's height trivially. Measured on real generated cities the
 * cross-fall is then 0.0 % at every percentile. The price is that the pavement pinches back
 * to the kerb over a short ramp at each corner, and that region falls to the interior.
 *
 * See engine.streets.generation.SidewalkRing, which is what produces these.
 */
public readonly struct CapInsetEdge
{
    /**
     * Inset point near the edge's first vertex.
     */
    public readonly Vector3 Start;

    /**
     * Inset point near the edge's second vertex.
     */
    public readonly Vector3 End;

    public CapInsetEdge(in Vector3 start, in Vector3 end)
    {
        Start = start;
        End = end;
    }
}
