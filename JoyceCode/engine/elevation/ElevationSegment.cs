
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

        public float[,] Elevations { get; }

        public engine.geom.Rect2 Rect2;

#if false
        /**
            * The minimal x position of this elevation rectangle.
            * This position is after the previous grid entry, but before or
            * at the first grid entry.
            */
        public float X0;

        /**
            * The minimal z position of this elevation rectangle.
            * This position is after the previous grid entry, but before or
            * at the first grid entry.
            */
        public float Z0;

        /**
            * The maximal x position of the elevation rectangle.
            */
        public float X1;

        /**
            * the maximal x position of this elevation rectangle.
            */
        public float Z1;
#endif

        /**
            * The number of horizontal raster points publically available in this grid.
            */
        public int nHoriz { get; }

        /**
            * The number of vertical raster points publically available in this grid.
            */
        public int nVert { get; }

        public ElevationSegment(
            int nHoriz0, int nVert0
        )
        {
            Rect2 = new();
            nHoriz = nHoriz0;
            nVert = nVert0;
            Elevations = new float[nVert, nHoriz];
        }
    }
}
