using System;
using System.Numerics;
using System.Collections.Generic;

namespace Splash.systems
{
    [DefaultEcs.System.With(typeof(engine.joyce.components.Skybox))]
    [DefaultEcs.System.With(typeof(Splash.components.PfMesh))]
    [DefaultEcs.System.With(typeof(Splash.components.PfMaterial))]
    sealed class DrawSkyboxesSystem : DefaultEcs.System.AEntitySetSystem<CameraOutput>
    {
        private engine.Engine _engine;
        private MaterialManager _materialManager;
        public Vector3 CameraPosition;

        private void _appendSkyboxes(
            ReadOnlySpan<DefaultEcs.Entity> entities,
            in Vector3 vCameraPosition, 
            in CameraOutput cameraOutput)
        {
            // TXWTODO: Sort it by distance.
            // TXWTODO: We certainly allow transparency in the skyboxes, so start with the one far away.
            foreach (var eSkybox in entities)
            {
                // No transformation applied, just the camera.
                var cSkybox = eSkybox.Get<engine.joyce.components.Skybox>();
                if (0 == (cameraOutput.CameraMask & cSkybox.CameraMask))
                {
                    continue;
                }
                var rlMeshEntry = eSkybox.Get<Splash.components.PfMesh>().MeshEntry;
                var rlMaterialEntry = eSkybox.Get<Splash.components.PfMaterial>().MaterialEntry;
                var matrixSkybox = Matrix4x4.CreateTranslation(vCameraPosition);

                cameraOutput.AppendInstance(rlMeshEntry, rlMaterialEntry, matrixSkybox);

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
            _appendSkyboxes(entities, CameraPosition, cameraOutput);
        }

        public DrawSkyboxesSystem(engine.Engine engine)
            : base(engine.GetEcsWorld())
        {
            _engine = engine;
        }
    }
}
