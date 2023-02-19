// #if defined(PLATFORM_DESKTOP)
// #define GLSL_VERSION 330
// #else   // PLATFORM_RPI, PLATFORM_ANDROID, PLATFORM_WEB
// #define GLSL_VERSION            100
//#endif
using System;
using Raylib_CsLo;
using System.Numerics;
using System.Collections.Generic;
using System.Text;
using DefaultEcs;
using DefaultEcs.Resource;

namespace Karawan.platform.cs1.splash
{
    public class MaterialManager : AResourceManager<engine.joyce.Material, RlMaterialEntry>
    {

        private bool _haveDefaults;
        private TextureManager _textureManager;

        /**
         * Lock object for me.
         */
        private object _lo = new();

        /**
         * The global placeholder texture.
         */
        // private Texture _loadingTexture;
        private RlMaterialEntry _loadingMaterial;
        private RlShaderEntry _rlInstanceShaderEntry;

        private RLights _rlights;

        // private Dictionary<string, splash.RlMaterialEntry> _dictMaterials = new();


        private unsafe void _createDefaultShader()
        {
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
            _rlInstanceShaderEntry.RlShader.locs[(int)ShaderLocationIndex.SHADER_LOC_MATRIX_NORMAL] =
                Raylib.GetShaderLocationAttrib(_rlInstanceShaderEntry.RlShader, "matNormal");
#if true
            // Set default shader locations: attributes locations
            _rlInstanceShaderEntry.RlShader.locs[(int)ShaderLocationIndex.SHADER_LOC_VERTEX_POSITION] =
                Raylib.GetShaderLocation(_rlInstanceShaderEntry.RlShader, "vertexPosition");
            _rlInstanceShaderEntry.RlShader.locs[(int)ShaderLocationIndex.SHADER_LOC_VERTEX_TEXCOORD01] =
                Raylib.GetShaderLocation(_rlInstanceShaderEntry.RlShader, "vertexTexCoord");
            _rlInstanceShaderEntry.RlShader.locs[(int)ShaderLocationIndex.SHADER_LOC_VERTEX_COLOR] =
                Raylib.GetShaderLocation(_rlInstanceShaderEntry.RlShader, "vertexColor");

            // Set default shader locations: uniform locations
            // _rlInstanceShaderEntry.RlShader.locs[(int)ShaderLocationIndex.SHADER_LOC_MATRIX_MVP] =
            // Raylib.GetShaderLocation(_rlInstanceShaderEntry.RlShader, "mvp");
            _rlInstanceShaderEntry.RlShader.locs[(int)ShaderLocationIndex.SHADER_LOC_COLOR_DIFFUSE] =
                Raylib.GetShaderLocation(_rlInstanceShaderEntry.RlShader, "colDiffuse");
            _rlInstanceShaderEntry.RlShader.locs[(int)ShaderLocationIndex.SHADER_LOC_MAP_ALBEDO] =
                Raylib.GetShaderLocation(_rlInstanceShaderEntry.RlShader, "texture0");
            _rlInstanceShaderEntry.RlShader.locs[(int)ShaderLocationIndex.SHADER_LOC_MAP_NORMAL] =
                Raylib.GetShaderLocation(_rlInstanceShaderEntry.RlShader, "texture2");
#endif
            /* 
             * Test code: Set some ambient lighting:
             */
            if (true)
            {
                int ambientLoc = Raylib.GetShaderLocation(_rlInstanceShaderEntry.RlShader, "ambient");
                var colAmbient = new Vector4(0.9f, 0.6f, 0.9f, 1.0f);
                Raylib.SetShaderValue(
                    _rlInstanceShaderEntry.RlShader,
                    ambientLoc, colAmbient,
                    ShaderUniformDataType.SHADER_UNIFORM_VEC4);
            }
        }

        private unsafe void _createDefaultMaterial()
        {
            var loadingMaterial = new RlMaterialEntry();

            Image checkedImage = Raylib.GenImageChecked(2, 2, 1, 1, Raylib.RED, Raylib.GREEN);
            var loadingTexture = Raylib.LoadTextureFromImage(checkedImage);
            Raylib.UnloadImage(checkedImage);

            /*
             * Test code: Create one light.
             */
            var vecLight = new Vector3(50f, 50f, 20f);
            var vecZero = new Vector3(0, 0, 0);
            _rlights.CreateLight(RLights.LightType.LIGHT_DIRECTIONAL, vecLight, vecZero, 
                new Color(255,255,255,255), ref _rlInstanceShaderEntry.RlShader); 

            loadingMaterial.RlMaterial = Raylib.LoadMaterialDefault();
            loadingMaterial.RlMaterial.shader = _rlInstanceShaderEntry.RlShader;
            loadingMaterial.RlMaterial.maps[(int)Raylib.MATERIAL_MAP_DIFFUSE].texture = loadingTexture;
            // loadingMaterial.RlMaterial.maps[(int)Raylib.MATERIAL_MAP_DIFFUSE].color = Raylib.WHITE;

            _loadingMaterial = loadingMaterial;

        }

        private string _materialKey(in engine.joyce.Material jMaterial)
        {
            string key = "mat-";
            string texName;
            if( jMaterial.Texture==null )
            {
                texName = "(null)";
            } else
            {
                texName = jMaterial.Texture.Source;
            }
            key += texName;
            return key;
        }

        private unsafe RlMaterialEntry _createRlMaterialEntry(in engine.joyce.Material jMaterial)
        {
            RlMaterialEntry rlMaterialEntry = new RlMaterialEntry();

            RlTextureEntry rlTextureEntry = null;
            if (jMaterial.Texture != null)
            {
                rlTextureEntry = _textureManager.FindRlTexture(jMaterial.Texture);
                // TXWTODO: Add reference of this texture.
            }
            RlTextureEntry rlEmissiveTextureEntry = null;
            if (jMaterial.EmissiveTexture != null)
            {
                rlEmissiveTextureEntry = _textureManager.FindRlTexture(jMaterial.EmissiveTexture);
            } else
            {
                rlEmissiveTextureEntry = _textureManager.FindRlTexture(new engine.joyce.Texture("joyce://col00000000"));
            }

#if false
            /*
             * Test code: Create one light.
             */
            var vecLight = new Vector3(50f, 50f, 0f);
            var vecZero = new Vector3(0, 0, 0);
            _rlights.CreateLight(RLights.LightType.LIGHT_DIRECTIONAL, vecLight, vecZero,
                Raylib.WHITE, ref _rlInstanceShaderEntry.RlShader);
#endif
            rlMaterialEntry.RlMaterial = Raylib.LoadMaterialDefault();
            rlMaterialEntry.RlMaterial.shader = _rlInstanceShaderEntry.RlShader;
            if (null != rlTextureEntry)
            {
                rlMaterialEntry.RlMaterial.maps[(int)MaterialMapIndex.MATERIAL_MAP_ALBEDO].texture =
                    rlTextureEntry.RlTexture;
            } else
            {
                // This becomes the default color in the shader.
                rlMaterialEntry.RlMaterial.maps[(int)MaterialMapIndex.MATERIAL_MAP_ALBEDO].color =
                    new Color(
                        (byte) (jMaterial.AlbedoColor >> 16) & 0xff,
                        (byte) (jMaterial.AlbedoColor >> 8) & 0xff,
                        (byte) (jMaterial.AlbedoColor) & 0xff,
                        (byte) (jMaterial.AlbedoColor >> 24) & 0xff);
                    
            }

            {
                rlMaterialEntry.RlMaterial.maps[(int)MaterialMapIndex.MATERIAL_MAP_NORMAL].texture =
                    rlEmissiveTextureEntry.RlTexture;
            }    
            // loadingMaterial.RlMaterial.maps[(int)Raylib.MATERIAL_MAP_DIFFUSE].color = Raylib.WHITE;

            if(jMaterial.HasTransparency) { 
                rlMaterialEntry.HasTransparency = true;
            }
            return rlMaterialEntry;
        }

        protected override unsafe RlMaterialEntry Load(engine.joyce.Material jMaterial)
        {
            RlMaterialEntry rlMaterialEntry;
            rlMaterialEntry = _createRlMaterialEntry(jMaterial);
            return rlMaterialEntry;
        }

        protected override void OnResourceLoaded(
            in Entity entity, 
            engine.joyce.Material jMaterial, 
            RlMaterialEntry rlMaterialEntry)
        {
            entity.Set<components.RlMaterial>(new components.RlMaterial(rlMaterialEntry));
        }

#if false
        /**
         * Return a material entry for the given material.
         * This references all the textures used within.
         */
        public RlMaterialEntry FindRlMaterial(in engine.joyce.Material jMaterial)
        {
            if( null==jMaterial )
            {
                return null;
            }
            RlMaterialEntry rme;
            string matKey = _materialKey(jMaterial);
            // TXWTODO add reference
            lock(_lo)
            {
                if( !_dictMaterials.ContainsKey(matKey))
                {
                    /*
                     * Create new material.
                     */
                    rme = _createRlMaterialEntry(jMaterial);
                    // TXWTODO: create a "creating material" state to keep that mutex open for a while.
                    _dictMaterials.Add(matKey, rme);
                } else
                {
                    rme = _dictMaterials[matKey];
                }
            }
            return rme;
        }
#endif


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

        public MaterialManager(TextureManager textureManager)
        {
            _haveDefaults = false;
            _textureManager = textureManager;
            if (!_haveDefaults)
            {
                _haveDefaults = true;
                _rlights = new();
                _createDefaultShader();
                _createDefaultMaterial();
            }
        }
    }
}
