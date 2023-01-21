using System;
using Raylib_CsLo;

namespace Karawan.platform.cs1.splash.systems
{
    [DefaultEcs.System.With(typeof(engine.joyce.components.Instance3))]
    [DefaultEcs.System.With(typeof(engine.transform.components.Transform3ToWorldMatrix))]
    [DefaultEcs.System.Without(typeof(splash.components.RlMesh))]
    /**
     * Create a raylib mesh for every mesh defined.
     * Totally unoptimized.
     */
    sealed class CreateRlMeshesSystem : DefaultEcs.System.AEntitySetSystem<engine.Engine>
    {
        private engine.Engine _engine;

        private bool _haveDefaults;
        /**
         * The global placeholder texture.
         */
        private Texture _loadingTexture;
        private Material _loadingMaterial;

        protected override void PreUpdate(engine.Engine state)
        {
        }

        protected override void PostUpdate(engine.Engine state)
        {
        }

        protected override unsafe void Update(engine.Engine state, ReadOnlySpan<DefaultEcs.Entity> entities)
        {
            if( !_haveDefaults )
            {
                _haveDefaults = true;
                Image checkedImage = Raylib.GenImageChecked(2, 2, 1, 1, Raylib.RED, Raylib.GREEN);
                _loadingTexture = Raylib.LoadTextureFromImage(checkedImage);
                _loadingMaterial = Raylib.LoadMaterialDefault();
                _loadingMaterial.maps[(int)Raylib.MATERIAL_MAP_DIFFUSE].texture = _loadingTexture;
                // _loadingMaterial.maps[(int)Raylib.MATERIAL_MAP_DIFFUSE].color = Raylib.RED;

                Raylib.UnloadImage(checkedImage);
            }
            foreach (var entity in entities)
            {
                var cInstance3 = entity.Get<engine.joyce.components.Instance3>();

                var nMeshes = cInstance3.Meshes.Count;
                var nMeshMaterials = cInstance3.MeshMaterials.Count;
                
                if( nMeshes!=nMeshMaterials)
                {
                    Console.WriteLine("We have a problem.");
                    return;
                }
                var nMaterials = cInstance3.Materials.Count;

                for(var i=0; i<nMeshes; ++i)
                {
                    var jMesh = (engine.joyce.Mesh) cInstance3.Meshes[i];
                    var materialIndex = (int) cInstance3.MeshMaterials[i];
                    var jMaterial = (engine.joyce.Material) cInstance3.Materials[materialIndex];

                    // TXWTODO: We need a mesh cache to enable proper allocation and upload of maehgs
                    var rMesh = MeshGenerator.CreateRaylibMesh(jMesh);

                    Raylib.UploadMesh(&rMesh, false);
                    entity.Set<splash.components.RlMesh>( 
                        new splash.components.RlMesh(rMesh, _loadingMaterial));
                }
            }
        }

        public unsafe CreateRlMeshesSystem(engine.Engine engine)
            : base( engine.GetEcsWorld() )
        {
            _engine = engine;
            _haveDefaults = false;
        }
    }
}
;