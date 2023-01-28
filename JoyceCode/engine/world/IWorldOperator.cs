using Java.Util.Functions;
using System;
using System.Collections.Generic;
using System.Text;

namespace engine.world
{
    public interface IWorldOperator
    {
        public string WorldOperatorGetPath();

        public void WorldOperatorApply(world.MetaGen worldMetaGen);
    }
}
