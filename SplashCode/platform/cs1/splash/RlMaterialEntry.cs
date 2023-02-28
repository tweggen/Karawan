using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Karawan.platform.cs1.splash
{
    public class RlMaterialEntry
    {
        public Raylib_CsLo.Material RlMaterial;
        public engine.joyce.Material JMaterial;

        public bool HasRlMaterial()
        {
            return RlMaterial.shader.id != 0xffffffff;
        }

        public bool HasTransparency()
        {
            return JMaterial.HasTransparency;
        }

        public RlMaterialEntry(engine.joyce.Material jMaterial)
        {
            JMaterial = jMaterial;
            RlMaterial.shader.id = 0xffffffff;
        }
    }
}
