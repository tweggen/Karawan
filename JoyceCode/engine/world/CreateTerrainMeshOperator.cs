

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
