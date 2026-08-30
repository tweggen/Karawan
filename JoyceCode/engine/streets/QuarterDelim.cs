using System.Numerics;

namespace engine.streets
{
    /**
     * One edge of a city block, as QuarterGenerator traces it.
     *
     * A delimiter is an EDGE and not a corner, and the two halves of it belong to
     * different junctions. Stroke runs from StreetPoint to the next junction round the
     * block, and StartPoint is the block's corner AT that next junction - a section point
     * of it, offset from its centre by roughly half the carriageway.
     *
     * That is not the reading the names invite, and reading it the other way is not
     * visible: pairing StartPoint with StreetPoint gives a corner the height of a junction
     * at the far end of a whole street. Measured over the generated cities that is 70 to
     * 97 m away at the median and 135 m at the worst, against 7 to 12 m for the junction
     * the corner really stands on. On a 1 % slope that alone sinks the pavement below the
     * roadway at 41 % of corners.
     *
     * So the corner and the junction it belongs to are written together by SetCorner and
     * cannot be set apart.
     */
    public class QuarterDelim
    {
        /**
         * The junction this edge LEAVES.
         */
        public StreetPoint StreetPoint;

        /**
         * The street this edge runs along, from StreetPoint to CornerStreetPoint.
         */
        public Stroke Stroke;

        /**
         * The block's corner, in cluster plan coordinates.
         */
        public Vector2 StartPoint { get; private set; }

        /**
         * The junction StartPoint is a section point of - the junction the block's corner
         * stands on, and therefore the one whose height it takes.
         */
        public StreetPoint CornerStreetPoint { get; private set; }


        /**
         * @param v2Corner
         *     A section point of spCorner, in cluster plan coordinates.
         * @param spCorner
         *     The junction it came from.
         */
        public void SetCorner(in Vector2 v2Corner, StreetPoint spCorner)
        {
            StartPoint = v2Corner;
            CornerStreetPoint = spCorner;
        }


        public QuarterDelim()
        {
        }
    }
}
