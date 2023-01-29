using DefaultEcs;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

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
        private static object _lockInstance = new();
        private static GroundOperator _instance;
        public static GroundOperator Instance()
        {
            lock (_lockInstance)
            {
                if (_instance == null)
                {
                    _instance = new GroundOperator("mydear");
                }
            }
            return _instance;
        }

        public const float MinElevation = 100f;
        public const float MaxElevation = -10f;
        public const float MaxWidth = 30000f;
        public const float MaxHeight = 30000f;
        public Vector3 MaxPos;
        public Vector3 MinPos;

        private int _skeletonWidth;
        private int _skeletonHeight;

        private float[,] _skeletonElevations;

        private engine.RandomSource _rnd;
        private engine.world.MetaGen _worldMetaGen;

        /** 
         * Create the elevation skeleton for the map. Sub-terrains may further refine this elevation mesh.
         */
        private void createSkeleton()
        {
            _rnd.clear();

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
            _skeletonElevations[y0,x0] = _rnd.getFloat() * (amplitude) + bias;
            _skeletonElevations[y0,x1] = _rnd.getFloat() * (amplitude) + bias;
            _skeletonElevations[y1,x0] = _rnd.getFloat() * (amplitude) + bias;
            _skeletonElevations[y1,x1] = _rnd.getFloat() * (amplitude) + bias;

            // This was the start, now refine.
            engine.elevation.Tools.RefineSkeletonElevation(
                0, 0,
                _skeletonElevations,
                MinElevation,
                MaxElevation,
                x0, y0, x1, y1);
        }

        public float[,] getSkeleton()
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
            _worldMetaGen = engine.world.MetaGen.Instance();

            MaxPos = new Vector3(MaxWidth / 2f - 1f, 0f, MaxHeight / 2f - 1f);
            MinPos = new Vector3(-MaxWidth / 2f + 1f, 0f, -MaxWidth / 2f + 1f);

            var fragmentSize = engine.world.MetaGen.FragmentSize;

            _skeletonWidth = (int) ((MaxWidth+fragmentSize-1)/fragmentSize ) + 1;
            _skeletonHeight = (int) ((MaxHeight+fragmentSize-1)/fragmentSize ) + 1;

            _rnd = new engine.RandomSource(seed0);
 
            createSkeleton();
        }
    }
}
