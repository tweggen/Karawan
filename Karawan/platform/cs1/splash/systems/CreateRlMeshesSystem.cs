using System;
using Raylib_CsLo;

namespace Karawan.platform.cs1.splash.systems
{
    [DefaultEcs.System.With(typeof(engine.joyce.components.Mesh))]
    [DefaultEcs.System.With(typeof(engine.transform.components.Object3ToWorldMatrix))]
    [DefaultEcs.System.Without(typeof(splash.components.RlMesh))]
    /**
     * Create a raylib mesh for every mesh defined.
     * Totally unoptimized.
     */
    sealed class CreateRlMeshesSystem : DefaultEcs.System.AEntitySetSystem<engine.Engine>
    {
        private engine.Engine _engine;

        protected override void PreUpdate(engine.Engine state)
        {
        }

        protected override void PostUpdate(engine.Engine state)
        {
        }

        protected override unsafe void Update(engine.Engine state, ReadOnlySpan<DefaultEcs.Entity> entities)
        {
#if true
            Image checkedImage = Raylib.GenImageChecked(2, 2, 1, 1, Raylib.RED, Raylib.GREEN);
            Texture texture = Raylib.LoadTextureFromImage(checkedImage);
            Raylib.UnloadImage(checkedImage);
#endif

#if false
                var rMaterial = new Raylib_CsLo.Material();
                rMaterial.maps = (Raylib_CsLo.MaterialMap*)
                    Raylib_CsLo.Raylib.MemAlloc((uint)
                        sizeof(Raylib_CsLo.MaterialMap) 
                        * (((int)(Raylib_CsLo.MaterialMapIndex.MATERIAL_MAP_BRDF)) + 1));
#endif

            foreach (var entity in entities)
            {
                var cMesh = entity.Get<engine.joyce.components.Mesh>();
                var rMesh = MeshGenerator.CreateRaylibMesh(cMesh);
#if false
                // Generating a material is wrong at this point, however, I need it for testing now.
                var rMaterial = new Material();
                // 12 matches the raylib define MAX_MATERIAL_MAP
                rMaterial.maps = (MaterialMap*)Raylib.MemAlloc((uint)(12*sizeof(MaterialMap)));
                rMaterial.maps[(int)Raylib.MATERIAL_MAP_DIFFUSE].texture = texture;
                // rMaterial.params = (float*)Raylib.MemAlloc((uint)(4 * sizeof(float)));
#else
                var rMaterial = Raylib.LoadMaterialDefault();
                rMaterial.maps[(int)Raylib.MATERIAL_MAP_DIFFUSE].texture = texture;
#endif

                Raylib.UploadMesh(&rMesh, false);
                entity.Set<splash.components.RlMesh>(
                    new splash.components.RlMesh(rMesh,rMaterial));
            }
        }

        public CreateRlMeshesSystem(engine.Engine engine)
            : base( engine.GetEcsWorld() )
        {
            _engine = engine;
        }
    }
}
;