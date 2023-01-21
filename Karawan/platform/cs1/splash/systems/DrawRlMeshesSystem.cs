using System;
using System.Numerics;

namespace Karawan.platform.cs1.splash.systems
{
    [DefaultEcs.System.With(typeof(engine.transform.components.Transform3ToWorldMatrix))]
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

        protected unsafe override void Update(engine.Engine state, ReadOnlySpan<DefaultEcs.Entity> entities)
        {
            foreach (var entity in entities)
            {                     
                var rMesh = entity.Get<splash.components.RlMesh>();
                var rMatrix = Matrix4x4.Transpose(entity.Get<engine.transform.components.Transform3ToWorldMatrix>().Matrix);
                Raylib_CsLo.Raylib.DrawMesh(rMesh.Mesh, rMesh.Material, rMatrix);
            }
        }

        public DrawRlMeshesSystem(engine.Engine engine)
            : base(engine.GetEcsWorld())
        {
            _engine = engine;
        }
    }
}
