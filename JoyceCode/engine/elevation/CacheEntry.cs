using System;

namespace engine.elevation
{
    public class CacheEntry
    {

        public float[,] elevations;


        /**
         * Return the interpolated height at the given relative position.
         */
        public float GetHeightAt(float x0, float z0)
        {
            if (null == elevations) {
                throw new NullReferenceException(
                    "elevation.CacheEntry.getHeightAt(): elevations still is null.");
            }

            var groundResolution = world.MetaGen.GroundResolution;
            float elevationStepSize =
                world.MetaGen.FragmentSize / (float) world.MetaGen.GroundResolution;

            int ex = (int) ((x0+world.MetaGen.FragmentSize/2.0) / elevationStepSize );
            int ey = (int) ((z0+world.MetaGen.FragmentSize/2.0) / elevationStepSize );

            if(ex<0 || ex> groundResolution ) {
                throw new ArgumentException( 
                    $"Invalid ex: {ex} > groundResolution: {groundResolution}, x0: {x0}" );
            }
            if(ey<0 || ey> groundResolution ) {
                throw new ArgumentException( 
                    $"Invalid ex: {ey} > groundResolution: {groundResolution}, z0: {z0}" );
            }

            /*
             * Now compute, where exactly within the triangles we are.
             */
            float y00 = elevations[ey,ex];
            float y01 = elevations[ey,ex + 1];
            float y10 = elevations[ey + 1,ex];
            float y11 = elevations[ey + 1,ex + 1];

            float tileX = (x0 + world.MetaGen.FragmentSize / 2f) - (elevationStepSize * ex);
            float tileY = (z0 + world.MetaGen.FragmentSize / 2f) - (elevationStepSize * ey);

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

            return yResult;
        }


        public CacheEntry()
        {
        }
    }    
}
