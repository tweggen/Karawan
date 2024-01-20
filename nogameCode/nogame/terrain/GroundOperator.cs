using DefaultEcs;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using engine.world;

namespace nogame.terrain
{
    /** 
     * Describes a generic terrain ground.
     *
     * The ground operator applies the seeded ground to the given terrain element.
     * To accelerate ground generatioin, it keeps an skeleton grid down to terrain
     * fragment size.
     */
    public class GroundOperator
    {
        private static readonly object _lo = new();
        private static GroundOperator _instance;
        public static GroundOperator Instance()
        {
            lock (_lo)
            {
                if (_instance == null)
                {
                    _instance = new GroundOperator("mydear");
                }
            }
            return _instance;
        }

        public const float MinElevation = 200f;
        public const float MaxElevation = -20f;

        private int _skeletonWidth;
        private int _skeletonHeight;

        private float[,] _skeletonElevations;

        public int SkeletonWidth
        {
            get => _skeletonWidth;
        }
        public int SkeletonHeight
        {
            get => _skeletonHeight;
        }
        
        private builtin.tools.RandomSource _rnd;

        /** 
         * Create the elevation skeleton for the map. Sub-terrains may further refine this elevation mesh.
         */
        private void _createSkeleton()
        {
            _rnd.Clear();

            _skeletonElevations = new float[_skeletonHeight,_skeletonWidth];

            /*
             * Now recursively generate.
             * First generate corner heights, then recursively refine.
             */
            int x0 = 0;
            int x1 = _skeletonWidth - 1;
            int y0 = 0;
            int y1 = _skeletonHeight - 1;

            float amplitude = MaxElevation - MinElevation;
            float bias = MinElevation;
            _skeletonElevations[y0,x0] = _rnd.GetFloat() * (amplitude) + bias;
            _skeletonElevations[y0,x1] = _rnd.GetFloat() * (amplitude) + bias;
            _skeletonElevations[y1,x0] = _rnd.GetFloat() * (amplitude) + bias;
            _skeletonElevations[y1,x1] = _rnd.GetFloat() * (amplitude) + bias;

            // This was the start, now refine.
            engine.elevation.Tools.RefineSkeletonElevation(
                0, 0,
                _skeletonElevations,
                MinElevation,
                MaxElevation,
                x0, y0, x1, y1,
                MetaGen.MaxSize);
        }

        public float[,] GetSkeleton()
        {
            return _skeletonElevations;
        }

        /**
         * Create the ground operator.
         *
         * Immediately after creation, it will compute a ground skeleton.
         */
        private GroundOperator(in string seed0)
        {
            var fragmentSize = engine.world.MetaGen.FragmentSize;
            var maxWidth = engine.world.MetaGen.MaxWidth;
            var maxHeight = engine.world.MetaGen.MaxHeight;

            _skeletonWidth = (int) ((maxWidth+fragmentSize-1)/fragmentSize ) + 1;
            _skeletonHeight = (int) ((maxHeight+fragmentSize-1)/fragmentSize ) + 1;

            _rnd = new builtin.tools.RandomSource(seed0);
 
            _createSkeleton();
        }
    }
}
