using System.Numerics;
using static engine.Logger;

namespace engine.elevation;

public class Tools
{
    private static float _damping = 0.4f;

    /**
     * Compute a reproducable random number for the given
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
        float rf = ri / (16f * 1024f * 1024f);
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
        float bias;
        float damping;

        if (v2Size.X > IndependentTopologyDistance)
        {
            bias = 0f;
            damping = 1f;
        }
        else
        {
            bias = (maxElevation - minElevation) / 2f;
            damping = _damping;
        }

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
            elevationArray[ym, x0] =
                (elevationArray[y0, x0] + elevationArray[y1, x0]) / 2f
                + _nrand(i, k, x0, ym) * amplitude + bias;
            RefineSkeletonElevation(i, k, elevationArray,
                minElevation * damping,
                maxElevation * damping,
                x0, y0, x1, ym, v2Size);
            RefineSkeletonElevation(i, k, elevationArray,
                minElevation * damping,
                maxElevation * damping,
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
            elevationArray[y0, xm] =
                (elevationArray[y0, x0] + elevationArray[y0, x1]) / 2f
                + _nrand(i, k, xm, y0) * amplitude + bias;
            RefineSkeletonElevation(i, k, elevationArray,
                minElevation * damping,
                maxElevation * damping,
                x0, y0, xm, y0, v2Size);
            RefineSkeletonElevation(i, k, elevationArray,
                minElevation * damping,
                maxElevation * damping,
                xm, y0, x1, y0, v2Size);
            return;
        }

        /*
         * Standard case, we can half the partition.
         */
        elevationArray[y0, xm] =
            (elevationArray[y0, x0] + elevationArray[y0, x1]) / 2f
            + _nrand(i, k, xm, y0) * amplitude + bias;
        elevationArray[ym, x0] =
            (elevationArray[y0, x0] + elevationArray[y1, x0]) / 2f
            + _nrand(i, k, x0, ym) * amplitude + bias;
        elevationArray[ym, x1] =
            (elevationArray[y0, x1] + elevationArray[y1, x1]) / 2f
            + _nrand(i, k, x1, ym) * amplitude + bias;
        elevationArray[y1, xm] =
            (elevationArray[y1, x0] + elevationArray[y1, x1]) / 2f
            + _nrand(i, k, xm, y1) * amplitude + bias;
        elevationArray[ym, xm] =
            (elevationArray[y0, xm] + elevationArray[y1, xm]
                                    + elevationArray[ym, x0] + elevationArray[ym, x1]) / 4f;

        /*
         * And generate subdivisions.
         */
        v2Size *= 0.5f;
        RefineSkeletonElevation(i, k, elevationArray,
            minElevation * damping,
            maxElevation * damping,
            x0, y0, xm, ym, v2Size);
        RefineSkeletonElevation(i, k, elevationArray,
            minElevation * damping,
            maxElevation * damping,
            xm, y0, x1, ym, v2Size);
        RefineSkeletonElevation(i, k, elevationArray,
            minElevation * damping,
            maxElevation * damping,
            x0, ym, xm, y1, v2Size);
        RefineSkeletonElevation(i, k, elevationArray,
            minElevation * damping,
            maxElevation * damping,
            xm, ym, x1, y1, v2Size);

        // That's it.
    }
}