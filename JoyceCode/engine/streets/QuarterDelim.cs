using System.Numerics;

namespace engine.streets
{
    /**
     * One edge of a city block, as QuarterGenerator traces it.
     *
     * A delimiter is a corner of the block plus the edge that LEAVES that corner:
     *
     * - StartPoint is the corner, a section point of StreetPoint offset outwards from it
     *   by roughly half a carriageway;
     * - StreetPoint is the junction the corner stands on;
     * - Stroke is the street the edge runs along, from StreetPoint to the NEXT
     *   delimiter's StreetPoint.
     *
     * So the block's boundary segment i runs from delims[i].StartPoint to
     * delims[i+1].StartPoint and lies alongside delims[i].Stroke, and everything about
     * delims[i] - its plan geometry, its height, its street and its junction - describes
     * that one segment.
     *
     * That was NOT true until this became one write. The generator used to fill the
     * delimiter in from two different steps of the trace: StartPoint from the junction the
     * edge arrived at and StreetPoint/Stroke from the one it left, a whole street apart.
     * Measured over the generated cities the boundary segment was 0.0 degrees off the NEXT
     * delimiter's stroke at 4.9 to 8.9 m - half a carriageway - and 60 to 76 degrees off
     * its own at 35 to 51 m, on 2936 of 2936 edges. Nothing about the names says which.
     *
     * Hence SetEdge and three private setters: the three cannot be written apart, so a
     * delimiter cannot describe two different edges again.
     */
    public class QuarterDelim
    {
        /**
         * The block's corner, in cluster plan coordinates.
         */
        public Vector2 StartPoint { get; private set; }

        /**
         * The junction StartPoint is a section point of - the junction the block's corner
         * stands on, and therefore the one whose height it takes.
         */
        public StreetPoint StreetPoint { get; private set; }

        /**
         * The street this edge runs along, leaving StreetPoint towards the next corner.
         */
        public Stroke Stroke { get; private set; }


        /**
         * @param v2Corner
         *     A section point of spCorner, in cluster plan coordinates.
         * @param spCorner
         *     The junction it came from.
         * @param strokeLeaving
         *     The street leaving spCorner towards the next corner of the block.
         */
        public void SetEdge(in Vector2 v2Corner, StreetPoint spCorner, Stroke strokeLeaving)
        {
            StartPoint = v2Corner;
            StreetPoint = spCorner;
            Stroke = strokeLeaving;
        }


        public QuarterDelim()
        {
        }
    }
}
