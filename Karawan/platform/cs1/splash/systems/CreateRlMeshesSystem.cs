// #if defined(PLATFORM_DESKTOP)
// #define GLSL_VERSION 330
// #else   // PLATFORM_RPI, PLATFORM_ANDROID, PLATFORM_WEB
// #define GLSL_VERSION            100
//#endif

using System;
using System.Text;
using System.Collections.Generic;
using Raylib_CsLo;


namespace Karawan.platform.cs1.splash.systems
{
    [DefaultEcs.System.With(typeof(engine.joyce.components.Instance3))]
    [DefaultEcs.System.With(typeof(engine.transform.components.Transform3ToWorld))]
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
        // private Texture _loadingTexture;
        private RlMaterialEntry _loadingMaterial;
        private RlShaderEntry _rlShaderEntry;

        private Dictionary<engine.joyce.Mesh, splash.RlMeshEntry> _dictMeshes;

        static private string _glslVersion = "330";

        private unsafe splash.RlMeshEntry _findRlMesh(engine.joyce.Mesh jMesh)
        {
            splash.RlMeshEntry rlMeshEntry;
            if ( _dictMeshes.TryGetValue(jMesh, out rlMeshEntry) )
            {
            } else
            {
                MeshGenerator.CreateRaylibMesh(jMesh, out rlMeshEntry);
                fixed (Raylib_CsLo.Mesh *pRlMeshEntry = &rlMeshEntry.RlMesh) {
                    Raylib.UploadMesh(pRlMeshEntry, false);
                }
                _dictMeshes.Add(jMesh, rlMeshEntry);
            }
            return rlMeshEntry;
        }

        protected override void PreUpdate(engine.Engine state)
        {
        }

        protected override void PostUpdate(engine.Engine state)
        {
        }

        private unsafe void _createDefaultMaterial()
        {
            var loadingMaterial = new RlMaterialEntry();
            var rlShaderEntry = new RlShaderEntry();

            rlShaderEntry.RlShader = Raylib.LoadShader(
                Raylib.TextFormat("resources/shaders/glsl%i/lighting_instancing.vs", _glslVersion),
                Raylib.TextFormat("resources/shaders/glsl%i/lighting.fs", _glslVersion));
            rlShaderEntry.RlShader.locs[(int)ShaderLocationIndex.SHADER_LOC_MATRIX_MVP] =
                Raylib.GetShaderLocation(rlShaderEntry.RlShader, "mvp");
            rlShaderEntry.RlShader.locs[(int)ShaderLocationIndex.SHADER_LOC_VECTOR_VIEW] =
                Raylib.GetShaderLocation(rlShaderEntry.RlShader, "viewPos");
            rlShaderEntry.RlShader.locs[(int)ShaderLocationIndex.SHADER_LOC_MATRIX_MODEL] =
                Raylib.GetShaderLocationAttrib(rlShaderEntry.RlShader, "instanceTransform");

            Image checkedImage = Raylib.GenImageChecked(2, 2, 1, 1, Raylib.RED, Raylib.GREEN);
            var loadingTexture = Raylib.LoadTextureFromImage(checkedImage);
            loadingMaterial.RlMaterial = Raylib.LoadMaterialDefault();
            loadingMaterial.RlMaterial.maps[(int)Raylib.MATERIAL_MAP_DIFFUSE].texture = loadingTexture;
            // loadingMaterial.maps[(int)Raylib.MATERIAL_MAP_DIFFUSE].color = Raylib.RED;
            Raylib.UnloadImage(checkedImage);
            _loadingMaterial = loadingMaterial;
            _rlShaderEntry = rlShaderEntry;
        }

        protected override void Update(engine.Engine state, ReadOnlySpan<DefaultEcs.Entity> entities)
        {
            if( !_haveDefaults )
            {
                _haveDefaults = true;
                _createDefaultMaterial();
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

                    var rlMeshEntry =_findRlMesh(jMesh);

                    // TXWTODO: Actually we don't need to copy it to the entity as we gather the meshes anyway, right?
                    entity.Set<splash.components.RlMesh>( 
                        new splash.components.RlMesh(rlMeshEntry, _loadingMaterial));
                }
            }
        }

        public unsafe CreateRlMeshesSystem(engine.Engine engine)
            : base( engine.GetEcsWorld() )
        {
            _engine = engine;
            _haveDefaults = false;
            _dictMeshes = new();
        }
    }
}
;