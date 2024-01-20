using System.Numerics;
using static engine.Logger;

namespace engine.elevation;

public class Tools
{
    private static float _damping = 0.4f;

    /**
     * Compute a reproducable random number from -1...1 for the given
     * grid position.
     */
    private static float _nrand(int i, int k, int x, int y)
    {
        int a, b;
        int n = world.MetaGen.GroundResolution;
        a = i * n;
        b = k * n;
        a += x;
        b += y;
        int ri = (((a * 1307 + b * 11) * (a * 3 + b * 16383) * (a * 401 + b * 19)) & 0xffffff0) >> 4;
        float rf = (ri / (16f * 1024f * 1024f) - 0.5f) * 2.0f;
        return rf;
    }


    /**
     * Above this grid point distance, elevations are not computed as interpolation
     * but as independent points.
     */
    public static readonly float IndependentTopologyDistance = 10000f;

    public static void RefineSkeletonElevation(
        int i, int k,
        in float[,] elevationArray,
        float minElevation, float maxElevation,
        int x0, int y0, int x1, int y1,
        Vector2 v2Size)
    {
        //Trace("Called minElevation: " + minElevation + ", maxElevation: " + maxElevation
        //      + ", x0: " + x0 + ", y0: " + y0 + ", x1: " + x1 + ", y1: " + y1);
        // Return, if there is nothing more to do.

        int xm = (int)((x0 + x1) / 2);
        int ym = (int)((y0 + y1) / 2);
        float amplitude = maxElevation - minElevation;
        float halfamp = amplitude / 2f;
        float bias = minElevation + halfamp;
        float weightNeighbours;
        float weightNew;
        float damping;
        float newMinElevation;
        float newMaxElevation;

        if (v2Size.X > IndependentTopologyDistance)
        {
           damping = 1f;
           weightNeighbours = 0f;
           weightNew = 1f;
        }
        else
        {
            damping = _damping;
            weightNeighbours = 1f;
            weightNew = 1f;
        }
        
        /*
         * Compute the new limits. If damping is zero, this exactly is the
         * same as the old limits.
         */
        //newMinElevation = bias - amplitude * damping / 2f;
        //newMaxElevation = bias + amplitude * damping / 2f;

        float newhalfamp = halfamp * damping;

        var fMidtotal = (float midNeighbours) =>
            (weightNeighbours * midNeighbours + (weightNew - weightNeighbours) * bias);
            
        /*
         * Special case: Only one column width?
         */
        if ((x0 + 1) >= x1)
        {
            if ((y0 + 1) >= y1)
            {
                return;
            }

            v2Size.Y *= 0.5f;
            float midTotal = fMidtotal((elevationArray[y0, x0] + elevationArray[y1, x0]) / 2f);
            elevationArray[ym, x0] = midTotal + _nrand(i, k, x0, ym) * halfamp;
            
            RefineSkeletonElevation(i, k, elevationArray,
                midTotal-newhalfamp, midTotal+newhalfamp,
                x0, y0, x1, ym, v2Size);
            RefineSkeletonElevation(i, k, elevationArray,
                midTotal-newhalfamp, midTotal+newhalfamp,
                x0, ym, x1, y1, v2Size);
            return;
        }

        /*
         * Only one row height?
         */
        if ((y0 + 1) >= y1)
        {
            if ((x0 + 1) >= x1)
            {
                return;
            }

            v2Size.X *= 0.5f;
            float midTotal = fMidtotal((elevationArray[y0, x0] + elevationArray[y0, x1]) / 2f);
            elevationArray[y0, xm] = midTotal + _nrand(i, k, xm, y0) * halfamp;
            RefineSkeletonElevation(i, k, elevationArray,
                midTotal-newhalfamp, midTotal+newhalfamp,
                x0, y0, xm, y0, v2Size);
            RefineSkeletonElevation(i, k, elevationArray,
                midTotal-newhalfamp, midTotal+newhalfamp,
                xm, y0, x1, y0, v2Size);
            return;
        }

        /*
         * Standard case, we can half the partition.
         */
        {
            float midTotalUpper = fMidtotal((elevationArray[y0, x0] + elevationArray[y0, x1]) / 2f);
            elevationArray[y0, xm] = midTotalUpper + _nrand(i, k, xm, y0) * halfamp;
            float midTotalLeft = fMidtotal((elevationArray[y0, x0] + elevationArray[y1, x0]) / 2f);
            elevationArray[ym, x0] = midTotalLeft + _nrand(i, k, x0, ym) * halfamp;
            float midTotalRight = fMidtotal((elevationArray[y0, x1] + elevationArray[y1, x1]) / 2f);
            elevationArray[ym, x1] = midTotalRight + _nrand(i, k, x1, ym) * halfamp;
            float midTotalBottom = fMidtotal((elevationArray[y1, x0] + elevationArray[y1, x1]) / 2f);
            elevationArray[y1, xm] = midTotalBottom + _nrand(i, k, xm, y1) * halfamp;
            float midTotalCenter = fMidtotal(
                (elevationArray[y0, xm]
                 + elevationArray[y1, xm]
                 + elevationArray[ym, x0]
                 + elevationArray[ym, x1]) / 4f);
            elevationArray[ym, xm] = midTotalCenter + _nrand(i, k, xm, ym) * halfamp;

            /*
             * And generate subdivisions.
             */
            v2Size *= 0.5f;
            float midTotalUL = fMidtotal(
                (elevationArray[y0, x0] 
                 + elevationArray[y0, xm]
                 + elevationArray[ym, x0] 
                 + elevationArray[ym, xm]) / 4f);
            RefineSkeletonElevation(i, k, elevationArray,
                midTotalUL-newhalfamp, midTotalUL+newhalfamp,
                x0, y0, xm, ym, v2Size);
            float midTotalUR = fMidtotal(
                (elevationArray[y0, xm] 
                 + elevationArray[y0, x1]
                 + elevationArray[ym, xm] 
                 + elevationArray[ym, x1]) / 4f);
            RefineSkeletonElevation(i, k, elevationArray,
                midTotalUR-newhalfamp, midTotalUR+newhalfamp,
                xm, y0, x1, ym, v2Size);
            float midTotalLL = fMidtotal(
                (elevationArray[ym, x0] 
                 + elevationArray[ym, xm]
                 + elevationArray[y1, x0] 
                 + elevationArray[y1, xm]) / 4f);
            RefineSkeletonElevation(i, k, elevationArray,
                midTotalLL-newhalfamp, midTotalLL+newhalfamp,
                x0, ym, xm, y1, v2Size);
            float midTotalLR = fMidtotal(
                (elevationArray[ym, xm] 
                 + elevationArray[ym, x1]
                 + elevationArray[y1, xm] 
                 + elevationArray[y1, x1]) / 4f);
            RefineSkeletonElevation(i, k, elevationArray,
                midTotalLR-newhalfamp, midTotalLR+newhalfamp,
                xm, ym, x1, y1, v2Size);

            // That's it.
        }
    }
}