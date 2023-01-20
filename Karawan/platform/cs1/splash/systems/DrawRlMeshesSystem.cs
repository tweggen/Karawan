using System;
using System.Numerics;

namespace Karawan.platform.cs1.splash.systems
{
    [DefaultEcs.System.With(typeof(engine.transform.components.Object3ToWorldMatrix))]
    [DefaultEcs.System.With(typeof(splash.components.RlMesh))]
    /**
     * Create a raylib mesh for every mesh defined.
     * Totally unoptimized.
     */
    sealed class DrawRlMeshesSystem : DefaultEcs.System.AEntitySetSystem<engine.Engine>
    {
        private engine.Engine _engine;

        protected override void PreUpdate(engine.Engine state)
        {
        }

        protected override void PostUpdate(engine.Engine state)
        {
        }

        protected override void Update(engine.Engine state, ReadOnlySpan<DefaultEcs.Entity> entities)
        {
            foreach (var entity in entities)
            {
                var rMaterial = new Raylib_CsLo.Material();

                var rMesh = entity.Get<splash.components.RlMesh>().Mesh;
                var rMatrix = Matrix4x4.Transpose(entity.Get<engine.transform.components.Object3ToWorldMatrix>().Matrix);
                Raylib_CsLo.Raylib.DrawMesh(rMesh, rMaterial, rMatrix);
            }
        }

        public DrawRlMeshesSystem(engine.Engine engine)
            : base(engine.GetEcsWorld())
        {
            _engine = engine;
        }
    }
}
