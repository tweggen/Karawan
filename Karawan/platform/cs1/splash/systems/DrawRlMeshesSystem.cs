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

        protected unsafe override void Update(engine.Engine state, ReadOnlySpan<DefaultEcs.Entity> entities)
        {
            foreach (var entity in entities)
            {
#if false
                var rMaterial = new Raylib_CsLo.Material();
                rMaterial.maps = (Raylib_CsLo.MaterialMap*)
                    Raylib_CsLo.Raylib.MemAlloc((uint)
                        sizeof(Raylib_CsLo.MaterialMap) 
                        * (((int)(Raylib_CsLo.MaterialMapIndex.MATERIAL_MAP_BRDF)) + 1));
#endif
                     
                var rMesh = entity.Get<splash.components.RlMesh>();
                // var rMatrix = Matrix4x4.Transpose(entity.Get<engine.transform.components.Object3ToWorldMatrix>().Matrix);
                var rMatrix = entity.Get<engine.transform.components.Object3ToWorldMatrix>().Matrix;
#if false
                Raylib_CsLo.Raylib.DrawMesh(
                    rMesh.Model.meshes[0],
                    rMesh.Model.materials[0],
                    rMatrix
                );
#endif
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
