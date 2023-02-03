using DefaultEcs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace nogame.terrain
{
    internal class ElevationBaseFactory : engine.elevation.IOperator
    {
        /// World width
        float _maxWidth;
        /// World height
        float _maxHeight;
        /// Number of indices horizontally in world.
        float _skeletonWidth;
        /// Number of indices vertically (depth) in world.
        float _skeletonHeight;

        private void trace(string message)
        {
            Console.WriteLine(message);
        }


        private string createSeed(int x, int y) 
        {
            return "fragxy-"+x+"_0_"+y;
        }


        /**
         * Compute the actual elevation array for a given grid entry.
         */
        private void _createElevationCacheEntry(
            int i, int k,
            in engine.elevation.Rect elevationRect)
        {

            GroundOperator groundOperator = GroundOperator.Instance();
            var skeletonElevations = groundOperator.getSkeleton();

            /*
             * Create a local array for computing the elevations. By convention,
             * it also includes the individual borders.
             */
            var nElevations = engine.world.MetaGen.GroundResolution + 1;
            float[,] localElevations = elevationRect.Elevations;

            /*
             * First setup the corners from the skeleton information.
             */

            /*
             * We try an API with the direct index coordinates for our global
             * precomputed elevation index.
             */
            int idxX = (int) (
                (i* engine.world.MetaGen.FragmentSize + _maxWidth/2.0)
                    / engine.world.MetaGen.FragmentSize );
            int idxY = (int) (
                (k* engine.world.MetaGen.FragmentSize + _maxHeight/2.0)
                    / engine.world.MetaGen.FragmentSize );

            int x0 = 0;
            int y0 = 0;
            int x1 = x0+nElevations-1;
            int y1 = y0+nElevations-1;

            if(true) {
                if( null==localElevations ) {
                    throw new InvalidOperationException($"localElevations is null");
                }
                if (null == skeletonElevations)
                {
                    throw new InvalidOperationException($"skeletonElevations is null");
                }
                if (y0 < 0)
                {
                    throw new InvalidOperationException($"y0 < 0");
                }
                if (y1 < 0)
                {
                    throw new InvalidOperationException($"y1 < 0");
                }
                if (x0 < 0)
                {
                    throw new InvalidOperationException($"x0 < 0");
                }
                if (x1 < 0)
                {
                    throw new InvalidOperationException($"x1 < 0");
                }
            }
            if (true)
            {
                if (idxX < 0)
                {
                    throw new InvalidOperationException($"idxX<0");
                }
                if (idxY < 0)
                {
                    throw new InvalidOperationException($"idxY<0");
                }
                idxY++;
                idxX++;
                --idxX;
                --idxY;
            }

            // trace('idxY $idxY idxX $idxX _skeletonHeight $_skeletonHeight');
            // trace(skeletonElevations.length);
            // trace(skeletonElevations[idxY+1].length);
            try
            {
                localElevations[y0, x0] = skeletonElevations[idxY, idxX];
                localElevations[y0, x1] = skeletonElevations[idxY, idxX + 1];
                localElevations[y1, x0] = skeletonElevations[idxY + 1, idxX];
                localElevations[y1, x1] = skeletonElevations[idxY + 1, idxX + 1];
            } catch(Exception e)
            {
                trace($"Caught exception {e}");
            }

            /*
             * Compute all remaining elevations.
             */
            engine.elevation.Tools.RefineSkeletonElevation(
                idxX, idxY,
                localElevations,
                GroundOperator.MinElevation,
                GroundOperator.MaxElevation,
                x0, y0, x1, y1);

        }


        public void ElevationOperatorProcess(
            in engine.elevation.IElevationProvider elevationInterface,
            in engine.elevation.Rect target
        )
        {
            /*
             * Copute the world fragment index- Remember we can asume this
             * exactly is one world fragment.
             */
            // TXWTODO: Create a convenicence function for this.
            var fs = engine.world.MetaGen.FragmentSize;
            int i = (int) Math.Floor((target.X0 + fs / 2.0) / fs);
            int k = (int) Math.Floor((target.Z0 + fs / 2.0) / fs);

            _createElevationCacheEntry(i, k, target);
        }


        public bool ElevationOperatorIntersects(
            float x0, float z0,
            float x1, float z1)
        {
            return true;
        }


        public ElevationBaseFactory()
        {
            _maxWidth = GroundOperator.MaxWidth;
            _maxHeight = GroundOperator.MaxHeight;

            var fragmentSize = engine.world.MetaGen.FragmentSize;

            _skeletonWidth = (int)((_maxWidth + fragmentSize - 1) / fragmentSize) + 1;
            _skeletonHeight = (int) ((_maxHeight + fragmentSize - 1) / fragmentSize) + 1;
        }    
    }
}
