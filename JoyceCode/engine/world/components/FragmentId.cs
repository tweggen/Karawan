using System;
using System.Collections.Generic;
using System.Text;

namespace engine.world.components
{
    public struct FragmentId
    {
        public int Id;

        public override string ToString()
        {
            return $"{base.ToString()}, Id={Id}";
        }

        public FragmentId(int id)
        {
            Id = id;
        }
    }
}
