using System;
using System.Numerics;
using System.Collections.Generic;


namespace Karawan.platform.cs1.splash.systems
{

    [DefaultEcs.System.With(typeof(engine.transform.components.Transform3ToWorld))]
    [DefaultEcs.System.With(typeof(splash.components.PfMesh))]
    [DefaultEcs.System.With(typeof(splash.components.PfMaterial))]
    /**
     * Render the platform meshes.
     * 
     * Groups by material and mesh.
     */
    sealed class DrawAMeshesSystem : DefaultEcs.System.AEntitySetSystem<CameraOutput>
    {
        private object _lo = new();
        private engine.Engine _engine;
        private MaterialManager _materialManager;

        private CameraOutput _cameraOutput = null;


        private void _appendMeshRenderList(
            in CameraOutput cameraOutput,
            in ReadOnlySpan<DefaultEcs.Entity> entities
        )
        {
            foreach (var entity in entities)
            {
                var transform3ToWorld = entity.Get<engine.transform.components.Transform3ToWorld>();
                if (0 != (transform3ToWorld.CameraMask & cameraOutput.CameraMask))
                {
                    var rlMeshEntry = entity.Get<splash.components.PfMesh>().MeshEntry;
                    var rlMaterialEntry = entity.Get<splash.components.PfMaterial>().MaterialEntry;


                    // Skip things that incompletely are loaded.
                    if( null==rlMeshEntry) {
                        continue;
                    }
                    if( null==rlMaterialEntry )
                    {
                        rlMaterialEntry = _materialManager.GetUnloadedMaterial();
                    }

                    var rMatrix = transform3ToWorld.Matrix;

                    cameraOutput.AppendInstance(rlMeshEntry, rlMaterialEntry, rMatrix);

                }
            }
        }


        protected override void PreUpdate(CameraOutput cameraOutput)
        {
        }

        protected override void PostUpdate(CameraOutput cameraOutput)
        {
        }


        protected override void Update(CameraOutput cameraOutput, ReadOnlySpan<DefaultEcs.Entity> entities)
        {
            _appendMeshRenderList(cameraOutput, entities);
        }


        public DrawAMeshesSystem(
            engine.Engine engine
        )
            : base(engine.GetEcsWorld())
        {
            _engine = engine;
            _cameraOutput = null;
        }
    }
}
