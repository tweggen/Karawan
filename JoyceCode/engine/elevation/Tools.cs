

namespace engine.elevation
{
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
            a = i* n;
            b = k* n;
            a += x;
            b += y;
            int ri = (((a*1307+b*11)*(a*3+b*16383)*(a*401+b*19)) & 0xffffff0)>>4;
            float rf = ri / (16f*1024f*1024f);
            return rf;
        }


        public static void RefineSkeletonElevation(
            int i, int k,
            in float[,] elevationArray,
            float minElevation, float maxElevation,
            int x0, int y0, int x1, int y1 )
        {
            //trace( "Called minElevation: "+minElevation+", maxElevation: "+maxElevation
            //    +", x0: "+x0+", y0: "+y0+", x1: "+x1+", y1: "+y1 );
            // Return, if there is nothing more to do.

            int xm = (int)((x0 + x1) / 2);
            int ym = (int)((y0 + y1) / 2);
            float amplitude = maxElevation - minElevation;
            float bias = -(maxElevation - minElevation) / 2f;

            // Special cases:
            if ((x0 + 1) >= x1)
            {
                if ((y0 + 1) >= y1)
                {
                    return;
                }
                elevationArray[ym,x0] =
                    (elevationArray[y0,x0] + elevationArray[y1,x0]) / 2f
                    + _nrand(i, k, x0, ym) * amplitude + bias;
                RefineSkeletonElevation(i, k, elevationArray,
                    minElevation * _damping,
                    maxElevation * _damping,
                    x0, y0, x1, ym);
                RefineSkeletonElevation(i, k, elevationArray,
                    minElevation * _damping,
                    maxElevation * _damping,
                    x0, ym, x1, y1);
                return;
            }
            if ((y0 + 1) >= y1)
            {
                if ((x0 + 1) >= x1)
                {
                    return;
                }
                elevationArray[y0,xm] =
                    (elevationArray[y0,x0] + elevationArray[y0,x1]) / 2f
                    + _nrand(i, k, xm, y0) * amplitude + bias;
                RefineSkeletonElevation(i, k, elevationArray,
                    minElevation * _damping,
                    maxElevation * _damping,
                    x0, y0, xm, y0);
                RefineSkeletonElevation(i, k, elevationArray,
                    minElevation * _damping,
                    maxElevation * _damping,
                    xm, y0, x1, y0);
                return;
            }

            elevationArray[y0,xm] =
                (elevationArray[y0,x0] + elevationArray[y0,x1]) / 2f
                + _nrand(i, k, xm, y0) * amplitude + bias;
            elevationArray[ym,x0] =
                (elevationArray[y0,x0] + elevationArray[y1,x0]) / 2f
                + _nrand(i, k, x0, ym) * amplitude + bias;
            elevationArray[ym,x1] =
                (elevationArray[y0,x1] + elevationArray[y1,x1]) / 2f
                + _nrand(i, k, x1, ym) * amplitude + bias;
            elevationArray[y1,xm] =
                (elevationArray[y1,x0] + elevationArray[y1,x1]) / 2f
                + _nrand(i, k, xm, y1) * amplitude + bias;
            elevationArray[ym,xm] =
                (elevationArray[y0,xm] + elevationArray[y1,xm]
                    + elevationArray[ym,x0] + elevationArray[ym,x1]) / 4f;

            // And generate subdivisions.
            RefineSkeletonElevation(i, k, elevationArray,
                minElevation * _damping,
                maxElevation * _damping,
                x0, y0, xm, ym);
            RefineSkeletonElevation(i, k, elevationArray,
                minElevation * _damping,
                maxElevation * _damping,
                xm, y0, x1, ym);
            RefineSkeletonElevation(i, k, elevationArray,
                minElevation * _damping,
                maxElevation * _damping,
                x0, ym, xm, y1);
            RefineSkeletonElevation(i, k, elevationArray,
                minElevation * _damping,
                maxElevation * _damping,
                xm, ym, x1, y1);

            // That's it.
        }
    }
}
