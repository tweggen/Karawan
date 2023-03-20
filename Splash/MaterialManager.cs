// #if defined(PLATFORM_DESKTOP)
// #define GLSL_VERSION 330
// #else   // PLATFORM_RPI, PLATFORM_ANDROID, PLATFORM_WEB
// #define GLSL_VERSION            100
//#endif
using System;
using System.Numerics;
using System.Collections.Generic;
using DefaultEcs;
using DefaultEcs.Resource;
using Material = engine.joyce.Material;

namespace Karawan.platform.cs1.splash
{
    public class MaterialManager : AResourceManager<engine.joyce.Material, AMaterialEntry>
    {
        private readonly IThreeD _threeD;
        
        /**
         * Lock object for me.
         */
        private object _lo = new();


        /**
         * Fill an already created material entry with its platform information.
         * This is executed from the rendering thread's context to 
         */
        public unsafe void FillMaterialEntry(in AMaterialEntry aMaterialEntry)
        {
            _threeD.FillMaterialEntry(aMaterialEntry);
        }
        

        protected override unsafe AMaterialEntry Load(engine.joyce.Material jMaterial)
        {
            AMaterialEntry aMaterialEntry = _threeD.CreateMaterialEntry(jMaterial);
            return aMaterialEntry;
        }
        

        protected override void OnResourceLoaded(
            in Entity entity, 
            engine.joyce.Material jMaterial, 
            AMaterialEntry aMaterialEntry)
        {
            entity.Set<components.PfMaterial>(new components.PfMaterial(aMaterialEntry));
        }
        

        protected override void Unload(Material jMaterial, AMaterialEntry aMaterialEntry)
        {
            base.Unload(jMaterial, aMaterialEntry);
            _threeD.UnloadMaterialEntry(aMaterialEntry);
        }
        

        public AMaterialEntry GetUnloadedMaterial()
        {
            return _threeD.GetDefaultMaterial();
        }
        
        
        public MaterialManager(in IThreeD threeD, TextureManager textureManager)
        {
            _threeD = threeD;
        }
    }
}
