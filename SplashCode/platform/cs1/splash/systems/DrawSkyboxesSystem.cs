using System;
using System.Numerics;
using System.Collections.Generic;

namespace Karawan.platform.cs1.splash.systems
{
    [DefaultEcs.System.With(typeof(engine.joyce.components.Skybox))]
    [DefaultEcs.System.With(typeof(splash.components.RlMesh))]
    [DefaultEcs.System.With(typeof(splash.components.RlMaterial))]
    sealed class DrawSkyboxesSystem : DefaultEcs.System.AEntitySetSystem<uint>
    {
        private engine.Engine _engine;
        private MaterialManager _materialManager;

        public Vector3 CameraPosition { get; set; }

        private void _drawSkyboxes(
            ReadOnlySpan<DefaultEcs.Entity> entities,
            in Vector3 vCameraPosition, 
            in uint cameraMasks)
        {
            // TXWTODO: Sort it by distance.
            // TXWTODO: We certainly allow transparency in the skyboxes, so start with the one far away.
            foreach (var eSkybox in entities)
            {
                // No transformation applied, just the camera.
                var cSkybox = eSkybox.Get<engine.joyce.components.Skybox>();
                if (0 == (cameraMasks & cSkybox.CameraMask))
                {
                    continue;
                }
                var rlMeshEntry = eSkybox.Get<splash.components.RlMesh>().MeshEntry;
                var rlMaterialEntry = eSkybox.Get<splash.components.RlMaterial>().MaterialEntry;
                var matrixSkybox = Matrix4x4.Transpose(
                    Matrix4x4.CreateTranslation(vCameraPosition));

                Matrix4x4[] arrMatrix = { matrixSkybox };
                Span<Matrix4x4> spanMatrix = arrMatrix;
                /*
                 * I must draw using the instanced call because I only use an instanced shader.
                 */
                Raylib_CsLo.Raylib.DrawMeshInstanced(
                    rlMeshEntry.RlMesh,
                    rlMaterialEntry.RlMaterial,
                    spanMatrix,
                    1
                );

            }
        }

        protected override void PreUpdate(uint cameraMask)
        {
        }

        protected override void PostUpdate(uint cameraMask)
        {
        }

        protected override void Update(uint cameraMask, ReadOnlySpan<DefaultEcs.Entity> entities)
        {
            _drawSkyboxes(entities, CameraPosition, cameraMask);
        }

        public DrawSkyboxesSystem(
            engine.Engine engine,
            MaterialManager materialManager
        )
            : base(engine.GetEcsWorld())
        {
            _engine = engine;
            _materialManager = materialManager;
        }
    }
}
