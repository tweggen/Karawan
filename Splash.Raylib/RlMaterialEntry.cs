using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Splash.Raylib
{
    public class RlMaterialEntry : AMaterialEntry
    {
        public Raylib_CsLo.Material RlMaterial;

        public override bool IsUploaded()
        {
            return RlMaterial.shader.id != 0xffffffff;
        }

        public override bool HasTransparency()
        {
            return JMaterial.HasTransparency;
        }

        public RlMaterialEntry(in engine.joyce.Material jMaterial)
            : base(jMaterial)
        {
            RlMaterial.shader.id = 0xffffffff;
        }
    }
}
