
using System;
using System.Collections.Generic;
using System.Text;

namespace engine.elevation
{
    /**
     * Carries an rectangular area of elevation data of the world.
     *
     * The resolution
     */
    public class ElevationSegment
    {

        public ElevationPixel[,] Elevations { get; }

        public engine.geom.Rect2 Rect2;

        /**
         * The number of horizontal raster points publically available in this grid.
         */
        public int nHoriz { get; }

        /**
            * The number of vertical raster points publically available in this grid.
            */
        public int nVert { get; }

        /**
         * Spacing of the samples. A segment carries GroundResolution + 1 samples spanning its
         * rectangle, so the first sits on one edge and the last on the other: the step is the
         * extent over nHoriz - 1, as ClusterConformElevationOperator worked out. Dividing by
         * nHoriz instead puts every sample short of where it is, by up to one step at the far
         * edge, restarting at every fragment (KAR-484).
         */
        public float StepX => nHoriz > 1 ? (Rect2.B.X - Rect2.A.X) / (nHoriz - 1) : 0f;
        public float StepZ => nVert > 1 ? (Rect2.B.Y - Rect2.A.Y) / (nVert - 1) : 0f;

        /** Where sample (tex, tez) is, in world X and Z. */
        public float SampleX(int tex) => Rect2.A.X + StepX * tex;
        public float SampleZ(int tez) => Rect2.A.Y + StepZ * tez;


        public ElevationSegment(
            int nHoriz0, int nVert0
        )
        {
            Rect2 = new();
            nHoriz = nHoriz0;
            nVert = nVert0;
            Elevations = new ElevationPixel[nVert, nHoriz];
        }
    }
}
