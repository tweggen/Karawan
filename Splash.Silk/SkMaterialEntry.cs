﻿using System;

namespace Splash.Silk
{
    public class SkMaterialEntry : AMaterialEntry
    {
        public SkTextureEntry SkDiffuseTexture;
        public SkTextureEntry SkEmissiveTexture;
        public bool _isUploaded = false; 

        public override bool IsUploaded()
        {
            return _isUploaded;
        }

        public void SetUploaded()
        {
            _isUploaded = true;
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