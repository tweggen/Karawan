
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
