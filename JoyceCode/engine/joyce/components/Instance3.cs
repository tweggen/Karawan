using System;
using System.Numerics;
using System.Collections.Generic;

namespace engine.joyce.components
{
    public struct Instance3
    {
        public InstanceDesc InstanceDesc;

        public override string ToString()
        {
            return $"{InstanceDesc.ToString()}";
        }

        /**
         * Construct a new instance3.
         * Caution: This uses the lists from the description.
         */
        public Instance3(in engine.joyce.InstanceDesc instanceDesc)
        {
            InstanceDesc = instanceDesc;
        }
    }
}
