using System;

namespace Splash.Silk
{
    public class SkMaterialEntry : AMaterialEntry
    {
        //public Raylib_CsLo.Material RlMaterial;

        public override bool IsUploaded()
        {
            //return RlMaterial.shader.id != 0xffffffff;
            return true;
        }

        public override bool HasTransparency()
        {
            return JMaterial.HasTransparency;
        }

        public SkMaterialEntry(in engine.joyce.Material jMaterial)
            : base(jMaterial)
        {
            //RlMaterial.shader.id = 0xffffffff;
        }
    }
}