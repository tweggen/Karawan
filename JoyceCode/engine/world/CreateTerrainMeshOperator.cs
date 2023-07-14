

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace engine.world
{
    internal class CreateTerrainMeshOperator : IFragmentOperator
    {
        // private var _rnd: engine.RandomSource;
        private string _myKey;

        public string FragmentOperatorGetPath()
        {
            // TXWTODO: The mesh should be generated very late, after everybody applied their terrain operators.
            return $"4005/CreateTerrainMeshOperator/{_myKey}/";
        }


        /**
         * This operator shall execute on every fragment.
         */
        public void FragmentOperatorGetAABB(out geom.AABB aabb)
        {
            aabb = MetaGen.AABB;
        }
        
        
        /**
         * Load the final elevation table from the elevation cache and
         * apply it to the world fragment.
         */
        public void FragmentOperatorApply(
           in world.Fragment worldFragment )
        {
            /*
             * Create geometry from the elevations stored in the fragment.
             */
            {
                worldFragment.WorldFragmentLoadGround();
            }
        }


        public CreateTerrainMeshOperator(string strKey)
        {
            _myKey = strKey;
        }
    }
}
