using System;
using System.Numerics;

namespace engine.elevation
{
    public class CacheEntry
    {

        public ElevationPixel[,] elevations;


        /**
         * Return the interpolated height at the given relative position
         *
         * @param v3Pos
         *     The position of the elevation pixel relative to the center of the fragment.
         */
        public ElevationPixel GetElevationPixelAt(in Vector3 v3Pos)
        {
            if (null == elevations) {
                throw new NullReferenceException(
                    "elevation.CacheEntry.getHeightAt(): elevations still is null.");
            }

            var groundResolution = world.MetaGen.GroundResolution;
            float elevationStepSize =
                world.MetaGen.FragmentSize / (float) world.MetaGen.GroundResolution;

            int ex = (int) ((v3Pos.X+world.MetaGen.FragmentSize/2.0) / elevationStepSize );
            int ey = (int) ((v3Pos.Z+world.MetaGen.FragmentSize/2.0) / elevationStepSize );

            if(ex<0 || ex> groundResolution ) {
                throw new ArgumentException( 
                    $"Invalid ex: {ex} > groundResolution: {groundResolution}, x0: {v3Pos.X}" );
            }
            if(ey<0 || ey> groundResolution ) {
                throw new ArgumentException( 
                    $"Invalid ex: {ey} > groundResolution: {groundResolution}, z0: {v3Pos.Z}" );
            }

            var epxOrg = elevations[ey, ex];
            
            /*
             * Now compute, where exactly within the triangles we are.
             */
            float y00 = elevations[ey,ex].Height;
            float y01 = elevations[ey,ex + 1].Height;
            float y10 = elevations[ey + 1,ex].Height;
            float y11 = elevations[ey + 1,ex + 1].Height;

            float tileX = (v3Pos.X + world.MetaGen.FragmentSize / 2f) - (elevationStepSize * ex);
            float tileY = (v3Pos.Z + world.MetaGen.FragmentSize / 2f) - (elevationStepSize * ey);

            // Which of the triangles is it?
            float yResult;
            if ((tileX + tileY) <= elevationStepSize)
            {
                // Upper half.
                yResult = y00;
                yResult += (y01 - y00) * tileX / elevationStepSize;
                yResult += (y10 - y00) * tileY / elevationStepSize;
            }
            else
            {
                // Lower half, inversed calculation.
                yResult = y11;
                yResult += (y10 - y11) * (elevationStepSize - tileX) / elevationStepSize;
                yResult += (y01 - y11) * (elevationStepSize - tileY) / elevationStepSize;
            }

            return epxOrg with { Height = yResult };
        }


        public CacheEntry()
        {
        }
    }    
}
