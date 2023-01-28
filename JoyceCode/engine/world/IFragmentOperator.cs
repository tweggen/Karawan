using Java.Util.Functions;
using System;
using System.Collections.Generic;
using System.Text;

namespace JoyceCode.engine.world
{
    public interface IFragmentOperator
    {

        /**
         * Return the path describing the order of the operator.
         * This is a preliminary operator until we found some generic way
         * to describe the tree-like structure the operators live in.
         *
         * The order of the fragment operators is determined by the order
         * of their pathes.
         * 
         * The pathes also are used as keys for the various internal
         * pipelines.
         */
        public string FragmentOperatorGetPath();


        /**
         * Apply this operator to a world fragment.
         *
         * This operator shall be stateless. That means, for a certain world
         * fragment in a given state and a certain configuratino, it shall generate
         * the same output.
         */
        public void FragmentOperatorApply(worldFragment: world.Fragment);

    }
}
