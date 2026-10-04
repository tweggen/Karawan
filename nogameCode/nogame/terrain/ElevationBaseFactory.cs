using System;
using System.Numerics;
using engine.elevation;
using engine.geom;
using static engine.Logger;

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

        private engine.geom.AABB _aabb;

        private string createSeed(int x, int y) 
        {
            return "fragxy-"+x+"_0_"+y;
        }


        /**
         * Compute the actual elevation array for a given grid entry.
         *
         * I believe this is essentially the content for a single fragment.
         */
        private void _createElevationCacheEntry(
            int i, int k,
            in engine.elevation.ElevationSegment elevationSegment)
        {

            GroundOperator groundOperator = GroundOperator.Instance();
            var skeletonElevations = groundOperator.GetSkeleton();

            /*
             * Create a local array for computing the elevations. By convention,
             * it also includes the individual borders.
             */
            var nElevations = engine.world.MetaGen.GroundResolution + 1;
            float[,] localElevations = new float[nElevations, nElevations]; 
            //= elevationSegment.Elevations;

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
                Error($"Caught exception {e}");
            }

            /*
             * Compute all remaining elevations.
             */
            engine.elevation.Tools.RefineSkeletonElevation(
                idxX, idxY,
                localElevations,
                GroundOperator.MinElevation,
                GroundOperator.MaxElevation,
                x0, y0, x1, y1,
                engine.world.MetaGen.FragmentSize2);

            /*
             * Create the entire default ElevationPixel info from the local array.
             *
             * Both loops are inclusive. A segment carries GroundResolution + 1 samples per
             * dimension, the last of which is the boundary shared with the next fragment,
             * and leaving it out does not leave a gap - it leaves a row of default
             * ElevationPixel, i.e. a height of exactly zero.
             *
             * That went unnoticed for a long time because almost nothing reads it. The
             * terrain mesh comes from Cache._elevationCacheGetRectAt, which stitches
             * indices k*gr .. (k+1)*gr-1 out of each fragment and takes the boundary sample
             * from the NEXT fragment's index 0, so the drawn ground never touched this row;
             * and every operator above the base layer refills its whole target from
             * GetElevationSegmentBelow, which is that same stitcher, so inside a city the
             * row was written from the neighbour. What was left is
             * CacheEntry.GetElevationPixelAt, which reads elevations[ey+1, ex] directly:
             * outside every cluster, a point query in the last 20 m strip of a fragment
             * interpolated between a real height and zero. That is Loader.GetHeightAt, so
             * ClusterDesc.GroundHeightAt, GetWalkingHeightAt, the hover probe's terrain
             * fallback and debris placement all read it.
             *
             * Note the index order: the array is [ez, ex] for every reader, so y indexes
             * rows and x indexes columns. The two were the other way round here, which is
             * why the dimension the exclusive loop dropped was the last Z row rather than
             * the last X column.
             *
             * JoyceCode.Tests ElevationGridCoverageTests measures all of the above against
             * the real cache, and scans this loop so the row cannot go missing again.
             */
            for (int y=y0; y<=y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    elevationSegment.Elevations[y, x] = new ElevationPixel()
                    {
                        Height = localElevations[y, x],
                        Biome = 0,
                        Flags1 = 0
                    };
                }
            }
        }


        public void ElevationOperatorProcess(
            in engine.elevation.IElevationProvider elevationInterface,
            in engine.elevation.ElevationSegment esTarget
        )
        {
            /*
             * Copute the world fragment index- Remember we can asume this
             * exactly is one world fragment.
             */
            // TXWTODO: Create a convenicence function for this.
            var fs = engine.world.MetaGen.FragmentSize;
            int i = (int) Math.Floor((esTarget.Rect2.A.X + fs / 2.0) / fs);
            int k = (int) Math.Floor((esTarget.Rect2.A.Y + fs / 2.0) / fs);

            _createElevationCacheEntry(i, k, esTarget);
        }


        public bool ElevationOperatorIntersects(engine.geom.AABB aabb)
        {
            return _aabb.IntersectsXZ(aabb);
        }


        public ElevationBaseFactory()
        {
            _maxWidth = engine.world.MetaGen.MaxWidth;
            _maxHeight = engine.world.MetaGen.MaxHeight;
            _aabb = new AABB(
                new Vector3(-_maxWidth / 2f, 0f, -_maxHeight / 2f),
                new Vector3(+_maxWidth / 2f, 0f, +_maxHeight / 2f)); 

            var fragmentSize = engine.world.MetaGen.FragmentSize;

            _skeletonWidth = (int)((_maxWidth + fragmentSize - 1) / fragmentSize) + 1;
            _skeletonHeight = (int) ((_maxHeight + fragmentSize - 1) / fragmentSize) + 1;
        }    
    }
}
