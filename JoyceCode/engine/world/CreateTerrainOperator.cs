using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace engine.world
{
    internal class CreateTerrainOperator : IFragmentOperator
    {
        private string _myKey;


        public string FragmentOperatorGetPath()
        {
            // TXWTODO: The mesh should be generated very late, after everybody applied their terrain operators.
            return $"4001/CreateTerrainOperator/{_myKey}/";
        }


        public void FragmentGetAABB(out Vector3 aa, out Vector3 bb)
        {
            aa = new Vector3(-MetaGen.MaxWidth / 2f, -1000f, -MetaGen.MaxHeight / 2f);
            bb = new Vector3(MetaGen.MaxWidth / 2f, 2000f, MetaGen.MaxHeight / 2f);
        }
        
        
        /**
         * Load the final elevation table from the elevation cache and
         * apply it to the world fragment.
         */
        public void FragmentOperatorApply(
            in world.Fragment worldFragment
        )
        {
            /*
             * Load the final elevation for that fragment from the elevation cache.
             * If not done before, this will trigger computing the elevation levels.
             * but most likely, some other operator will already have triggered this.
             */
            {
                var elevationCache = elevation.Cache.Instance();
                var i = worldFragment.IdxFragment.I;
                var k = worldFragment.IdxFragment.K;

                /*
                 * This would be the right way, however, it's more expensive.
                 */
                var fs = world.MetaGen.FragmentSize;
                float x0 = i * fs - fs / 2f;
                float z0 = k * fs - fs / 2f;
                float x1 = (i + 1) * fs - fs / 2f;
                float z1 = (k + 1) * fs - fs / 2f;

                elevation.Rect elevationRect = elevationCache.ElevationCacheGetRectBelow(
                    x0, z0, x1, z1, elevation.Cache.TOP_LAYER
                );
                worldFragment.WorldFragmentSetGroundArray(
                    elevationRect.Elevations,
                    world.MetaGen.GroundResolution,
                    0, 0, world.MetaGen.GroundResolution, world.MetaGen.GroundResolution,
                    0, 0);
            }
        }


        public CreateTerrainOperator(string strKey)
        {
            _myKey = strKey;
        }
    }
}
