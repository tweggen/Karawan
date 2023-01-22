// #if defined(PLATFORM_DESKTOP)
// #define GLSL_VERSION 330
// #else   // PLATFORM_RPI, PLATFORM_ANDROID, PLATFORM_WEB
// #define GLSL_VERSION            100
//#endif
using Raylib_CsLo;

namespace Karawan.platform.cs1.splash
{
    public class MaterialManager
    {
        // TXWTODO: Add a define for the GLSL platform
        static private string _glslVersion = "330";

        private bool _haveDefaults;
        /**
         * The global placeholder texture.
         */
        // private Texture _loadingTexture;
        private RlMaterialEntry _loadingMaterial;
        private RlShaderEntry _rlShaderEntry;


        private unsafe void _createDefaultMaterial()
        {
            var loadingMaterial = new RlMaterialEntry();

#if false
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
#endif
            Image checkedImage = Raylib.GenImageChecked(2, 2, 1, 1, Raylib.RED, Raylib.GREEN);
            var loadingTexture = Raylib.LoadTextureFromImage(checkedImage);
            loadingMaterial.RlMaterial = Raylib.LoadMaterialDefault();
            loadingMaterial.RlMaterial.maps[(int)Raylib.MATERIAL_MAP_DIFFUSE].texture = loadingTexture;
            // loadingMaterial.maps[(int)Raylib.MATERIAL_MAP_DIFFUSE].color = Raylib.RED;
            Raylib.UnloadImage(checkedImage);

            _loadingMaterial = loadingMaterial;

            //_rlShaderEntry = rlShaderEntry;
        }


        public RlMaterialEntry FindRlMaterial(engine.joyce.Material jMaterial)
        {
            return _loadingMaterial;
        }


        public RlMaterialEntry GetUnloadedMaterial()
        {
            return _loadingMaterial;
        }


        public MaterialManager()
        {
            _haveDefaults = false;
            if (!_haveDefaults)
            {
                _haveDefaults = true;
                _createDefaultMaterial();
            }
        }
    }
}
