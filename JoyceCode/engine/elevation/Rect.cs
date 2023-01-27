using Java.Lang;
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
    public class Rect
    {

        public float[,] elevations { get; }

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

        /**
            * The number of horizontal raster points publically available in this grid.
            */
        public int nHoriz { get; }

        /**
            * The number of vertical raster points publically available in this grid.
            */
        public int nVert { get; }

        public Rect(
            int nHoriz0, int nVert0
        ) {
            X0 = 0f;
            Z0 = 0f;
            X1 = 0f;
            Z1 = 0f;
            nHoriz = nHoriz0;
            nVert = nVert0;
            elevations = new float[nVert, nHoriz];
        }
    }
}
