using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Raylib_CsLo;
using static engine.Logger;

namespace Karawan.platform.cs1.splash;

public class RaylibThreeD : IThreeD
{
    private readonly engine.Engine _engine;
    private object _lo = new();
    
    private RlMaterialEntry _loadingMaterial; 
    private readonly TextureManager _textureManager;

    private RlShaderEntry _rlInstanceShaderEntry;
    
    public class LightShaderPos
    {
        // Shader locations
        public int enabledLoc;
        public int typeLoc;
        public int posLoc;
        public int targetLoc;
        public int colorLoc;
    }

    /*
     * As the ambient lights are not per light but per total, we
     * have an extra location in the shader.
     */
    private int _ambientLoc;

    // TXWTODO: Ugly data structure
    private LightShaderPos[] _lightShaderPos = null;
    
    // Create a light and get shader locations
    private void _compileLightLocked(
        in LightShaderPos lightShaderPos, int lightIndex, ref Shader shader)
    {
        // TODO: Below code doesn't look good to me, 
        // it assumes a specific shader naming and structure
        // Probably this implementation could be improved
        string enabledName = $"lights[{lightIndex}].enabled";
        string typeName = $"lights[{lightIndex}].type";
        string posName = $"lights[{lightIndex}].position";
        string targetName = $"lights[{lightIndex}].target";
        string colorName = $"lights[{lightIndex}].color";

        // Set location name [x] depending on lights count
        //enabledName[7] = '0' + lightsCount;
        //typeName[7] = '0' + lightsCount;
        //posName[7] = '0' + lightsCount;
        //targetName[7] = '0' + lightsCount;
        //colorName[7] = '0' + lightsCount;

        lightShaderPos.enabledLoc = Raylib.GetShaderLocation(shader, enabledName);
        lightShaderPos.typeLoc = Raylib.GetShaderLocation(shader, typeName);
        lightShaderPos.posLoc = Raylib.GetShaderLocation(shader, posName);
        lightShaderPos.targetLoc = Raylib.GetShaderLocation(shader, targetName);
        lightShaderPos.colorLoc = Raylib.GetShaderLocation(shader, colorName);

    }


    private LightShaderPos _getLightShaderPos(int index, ref Shader shader)
    {
        lock (_lo)
        {
            if (null == _lightShaderPos)
            {
                _lightShaderPos = new LightShaderPos[LightManager.MAX_LIGHTS];
                for (int i = 0; i < LightManager.MAX_LIGHTS; ++i)
                {
                    _lightShaderPos[i] = new();
                    _compileLightLocked(_lightShaderPos[i], i, ref shader);
                }
                _ambientLoc = Raylib.GetShaderLocation(shader, "ambient");

            }

            return _lightShaderPos[index];
        }
    }
    
    
    /**
         * Update lights value in shader
         */
    private unsafe void _applyLightValues(ref Shader shader, int index, in Light light)
    {
        var lightShaderPos = _getLightShaderPos(index, ref shader);
        fixed (Light* pLight = &light)
        {
            // Send to shader light enabled state and type
            Raylib.SetShaderValue(shader, lightShaderPos.enabledLoc, &pLight->enabled, ShaderUniformDataType.SHADER_UNIFORM_INT);
            Raylib.SetShaderValue(shader, lightShaderPos.typeLoc, &pLight->type, ShaderUniformDataType.SHADER_UNIFORM_INT);

            // Send to shader light position values
            Vector3 position = new(light.position.X, light.position.Y, light.position.Z);
            Raylib.SetShaderValue(shader, lightShaderPos.posLoc, position, ShaderUniformDataType.SHADER_UNIFORM_VEC3);

            // Send to shader light target position values
            Vector3 target = new(light.target.X, light.target.Y, light.target.Z);
            Raylib.SetShaderValue(shader, lightShaderPos.targetLoc, target, ShaderUniformDataType.SHADER_UNIFORM_VEC3);

            // Send to shader light color values
            Vector4 color = new((float)light.color.X / (float)255, (float)light.color.Y / (float)255,
                (float)light.color.Z / (float)255, (float)light.color.W / (float)255);
            Raylib.SetShaderValue(shader, lightShaderPos.colorLoc, color, ShaderUniformDataType.SHADER_UNIFORM_VEC4);
        }
    }


    private void _applyAllLights(in IList<Light> listLights, ref Shader shader)
    {
        for (int i = 0; i < listLights.Count; i++)
        {
            _applyLightValues(ref shader, i, listLights[i]);                
        }
    }

    public void ApplyAllLights(in IList<Light> listLights, in AShaderEntry aShaderEntry)
    {
        _applyAllLights(listLights, ref ((RlShaderEntry)aShaderEntry).RlShader);
    }
    
    
    public void ApplyAmbientLights(in Vector4 colAmbient, in AShaderEntry aShaderEntry)
    {
        Raylib.SetShaderValue(
            ((RlShaderEntry)aShaderEntry).RlShader,
            _ambientLoc, colAmbient,
            ShaderUniformDataType.SHADER_UNIFORM_VEC4);
    }


    
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

        /* 
         * Test code: Set some ambient lighting:
         */
        // SetAmbientLight(new Vector4(0.9f, 0.6f, 0.9f, 1.0f));
        
    }

    public RlShaderEntry GetInstanceShaderEntry()
    {
        return _rlInstanceShaderEntry;
    }
    
    
    public void DrawMeshInstanced(
        in AMeshEntry aMeshEntry, 
        in AMaterialEntry aMaterialEntry, 
        in Span<Matrix4x4> spanMatrices,
        in int nMatrices)
    {
        Raylib_CsLo.Raylib.DrawMeshInstanced(
                ((RlMeshEntry)aMeshEntry).RlMesh,
                ((RlMaterialEntry)aMaterialEntry).RlMaterial,
                spanMatrices, 
                nMatrices
        );
    }   

    public unsafe void UploadMesh(in AMeshEntry aMeshEntry)
    {
        RlMeshEntry rlMeshEntry = (RlMeshEntry)aMeshEntry;
        fixed (Raylib_CsLo.Mesh* pRlMeshEntry = &rlMeshEntry.RlMesh)
        {
            Raylib.UploadMesh(pRlMeshEntry, false);
        }
        Trace($"Uploaded Mesh vaoId={rlMeshEntry.RlMesh.vaoId}, nVertices={rlMeshEntry.RlMesh.vertexCount}");

    }

    public AMeshEntry CreateMeshEntry(in engine.joyce.Mesh jMesh)
    {
        RlMeshEntry rlMeshEntry;
        MeshGenerator.CreateRaylibMesh(jMesh, out rlMeshEntry);
        return rlMeshEntry;
    }

    public void DestroyMeshEntry(in AMeshEntry aMeshEntry)
    {
        RlMeshEntry rlMeshEntry = (RlMeshEntry)aMeshEntry;
        _engine.QueueCleanupAction(() =>
        {
            Trace($"Unloading Mesh vaoId={rlMeshEntry.RlMesh.vaoId}, nVertices={rlMeshEntry.RlMesh.vertexCount}");
            Raylib.UnloadMesh(rlMeshEntry.RlMesh);
        });
    }

    public unsafe AMaterialEntry GetDefaultMaterial()
    {
        lock (_lo)
        {
            if (_loadingMaterial == null)
            {
                var loadingMaterial = new RlMaterialEntry(new engine.joyce.Material());

                Image checkedImage = Raylib.GenImageChecked(2, 2, 1, 1, Raylib.RED, Raylib.GREEN);
                var loadingTexture = Raylib.LoadTextureFromImage(checkedImage);
                Raylib.UnloadImage(checkedImage);

                loadingMaterial.RlMaterial = Raylib.LoadMaterialDefault();
                loadingMaterial.RlMaterial.shader = _rlInstanceShaderEntry.RlShader;
                loadingMaterial.RlMaterial.maps[(int)Raylib.MATERIAL_MAP_DIFFUSE].texture = loadingTexture;
                // loadingMaterial.RlMaterial.maps[(int)Raylib.MATERIAL_MAP_DIFFUSE].color = Raylib.WHITE;
                _loadingMaterial = loadingMaterial;
            }

            return _loadingMaterial;
        }
    }

    public AMaterialEntry CreateMaterialEntry(in engine.joyce.Material jMaterial)
    {
        RlMaterialEntry rlMaterialEntry = new RlMaterialEntry(jMaterial);

        return rlMaterialEntry;
    }


    public unsafe void FillMaterialEntry(in AMaterialEntry aMaterialEntry)
    {
        RlMaterialEntry rlMaterialEntry = (RlMaterialEntry) aMaterialEntry;
        engine.joyce.Material jMaterial = rlMaterialEntry.JMaterial;
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
        }
        else
        {
            rlEmissiveTextureEntry = _textureManager.FindRlTexture(new engine.joyce.Texture("joyce://col00000000"));
        }

        rlMaterialEntry.RlMaterial = Raylib.LoadMaterialDefault();
        rlMaterialEntry.RlMaterial.shader = _rlInstanceShaderEntry.RlShader;
        if (null != rlTextureEntry)
        {
            rlMaterialEntry.RlMaterial.maps[(int)MaterialMapIndex.MATERIAL_MAP_ALBEDO].texture =
                rlTextureEntry.RlTexture;
        }
        else
        {
            // This becomes the default color in the shader.
            rlMaterialEntry.RlMaterial.maps[(int)MaterialMapIndex.MATERIAL_MAP_ALBEDO].color =
                new Color(
                    (byte)(jMaterial.AlbedoColor >> 16) & 0xff,
                    (byte)(jMaterial.AlbedoColor >> 8) & 0xff,
                    (byte)(jMaterial.AlbedoColor) & 0xff,
                    (byte)(jMaterial.AlbedoColor >> 24) & 0xff);

        }

        {
            rlMaterialEntry.RlMaterial.maps[(int)MaterialMapIndex.MATERIAL_MAP_NORMAL].texture =
                rlEmissiveTextureEntry.RlTexture;
        }
    }

    public void UnloadMaterialEntry(in AMaterialEntry aMaterialEntry)
    {
        // TWTODO: Actually write this. 
    }

    public unsafe void SetCameraPos(in Vector3 vCamera)
    {
        Raylib.SetShaderValue(
            _rlInstanceShaderEntry.RlShader,
            _rlInstanceShaderEntry.RlShader.locs[(int)ShaderLocationIndex.SHADER_LOC_VECTOR_VIEW],
            vCamera,
            ShaderUniformDataType.SHADER_UNIFORM_VEC3);
    }

    public RaylibThreeD(in engine.Engine engine, in TextureManager textureManager)
    {
        _engine = engine;
        _textureManager = textureManager;
        _createDefaultShader();
        GetDefaultMaterial();
    }
   
}