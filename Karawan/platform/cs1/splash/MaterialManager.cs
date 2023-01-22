// #if defined(PLATFORM_DESKTOP)
// #define GLSL_VERSION 330
// #else   // PLATFORM_RPI, PLATFORM_ANDROID, PLATFORM_WEB
// #define GLSL_VERSION            100
//#endif
using Raylib_CsLo;
using System.Numerics;
using System.Text;

namespace Karawan.platform.cs1.splash
{
    public class MaterialManager
    {
        // TXWTODO: Add a define for the GLSL platform
        static private int _glslVersion = 330;


        private bool _haveDefaults;
        /**
         * The global placeholder texture.
         */
        // private Texture _loadingTexture;
        private RlMaterialEntry _loadingMaterial;
        private RlShaderEntry _rlInstanceShaderEntry;

        private RLights _rlights;


        private unsafe void _createDefaultMaterial()
        {
            var loadingMaterial = new RlMaterialEntry();

            _rlInstanceShaderEntry = new RlShaderEntry();

            {
                byte[] byLightingInstancingVS = Encoding.ASCII.GetBytes(shadercode.LightingInstancingVS.ShaderCode);
                byte[] byLightingFS = Encoding.ASCII.GetBytes(shadercode.LightingFS.ShaderCode);
                fixed (byte* byLIVS = byLightingInstancingVS)
                {
                    fixed (byte* byLFS = byLightingFS)
                    {
                        _rlInstanceShaderEntry.RlShader = Raylib.LoadShaderFromMemory(
                            (sbyte*)byLIVS, (sbyte*)byLFS);
                    }

                }
            }
            _rlInstanceShaderEntry.RlShader.locs[(int)ShaderLocationIndex.SHADER_LOC_MATRIX_MVP] =
                Raylib.GetShaderLocation(_rlInstanceShaderEntry.RlShader, "mvp");
            _rlInstanceShaderEntry.RlShader.locs[(int)ShaderLocationIndex.SHADER_LOC_VECTOR_VIEW] =
                Raylib.GetShaderLocation(_rlInstanceShaderEntry.RlShader, "viewPos");
            _rlInstanceShaderEntry.RlShader.locs[(int)ShaderLocationIndex.SHADER_LOC_MATRIX_MODEL] =
                Raylib.GetShaderLocationAttrib(_rlInstanceShaderEntry.RlShader, "instanceTransform");

            /* 
             * Test code: Set some ambient lighting:
             */
            {
                int ambientLoc = Raylib.GetShaderLocation(_rlInstanceShaderEntry.RlShader, "ambient");
                var colAmbient = new Vector4(0.99f, 0.99f, 0.99f, 1.0f);
                Raylib.SetShaderValue(
                    _rlInstanceShaderEntry.RlShader,
                    ambientLoc, colAmbient, 
                    ShaderUniformDataType.SHADER_UNIFORM_VEC4);
            }
            Image checkedImage = Raylib.GenImageChecked(2, 2, 1, 1, Raylib.RED, Raylib.GREEN);
            var loadingTexture = Raylib.LoadTextureFromImage(checkedImage);
            Raylib.UnloadImage(checkedImage);

            /*
             * Test code: Create one light.
             */
            {
                var vecLight = new Vector3(50f, 50f, 0f);
                var vecZero = new Vector3(0, 0, 0);
                _rlights.CreateLight(RLights.LightType.LIGHT_DIRECTIONAL, vecLight, vecZero, 
                    Raylib.WHITE, _rlInstanceShaderEntry.RlShader); 
            }

            loadingMaterial.RlMaterial = Raylib.LoadMaterialDefault();
            loadingMaterial.RlMaterial.shader = _rlInstanceShaderEntry.RlShader;
            loadingMaterial.RlMaterial.maps[(int)Raylib.MATERIAL_MAP_DIFFUSE].texture = loadingTexture;
            // loadingMaterial.RlMaterial.maps[(int)Raylib.MATERIAL_MAP_DIFFUSE].color = Raylib.WHITE;

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

        public unsafe void HackSetCameraPos( in Vector3 vecCamera )
        {
            Raylib.SetShaderValue(
                _rlInstanceShaderEntry.RlShader,
                _rlInstanceShaderEntry.RlShader.locs[(int)ShaderLocationIndex.SHADER_LOC_VECTOR_VIEW],
                vecCamera,
                ShaderUniformDataType.SHADER_UNIFORM_VEC3);
        }

        public MaterialManager()
        {
            _haveDefaults = false;
            if (!_haveDefaults)
            {
                _haveDefaults = true;
                _rlights = new();
                _createDefaultMaterial();
            }
        }
    }
}
