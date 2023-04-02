using System.Runtime.InteropServices;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Raylib_CsLo;
using static engine.Logger;

namespace Splash.Raylib;

public class RaylibThreeD : IThreeD
{
    private readonly engine.Engine _engine;
    private object _lo = new();
    
    private RlMaterialEntry _loadingMaterial;
    private readonly TextureGenerator _textureGenerator;
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

        lightShaderPos.enabledLoc = Raylib_CsLo.Raylib.GetShaderLocation(shader, enabledName);
        lightShaderPos.typeLoc = Raylib_CsLo.Raylib.GetShaderLocation(shader, typeName);
        lightShaderPos.posLoc = Raylib_CsLo.Raylib.GetShaderLocation(shader, posName);
        lightShaderPos.targetLoc = Raylib_CsLo.Raylib.GetShaderLocation(shader, targetName);
        lightShaderPos.colorLoc = Raylib_CsLo.Raylib.GetShaderLocation(shader, colorName);

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
                _ambientLoc = Raylib_CsLo.Raylib.GetShaderLocation(shader, "ambient");

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
            Raylib_CsLo.Raylib.SetShaderValue(shader, lightShaderPos.enabledLoc, &pLight->enabled, ShaderUniformDataType.SHADER_UNIFORM_INT);
            Raylib_CsLo.Raylib.SetShaderValue(shader, lightShaderPos.typeLoc, &pLight->type, ShaderUniformDataType.SHADER_UNIFORM_INT);

            // Send to shader light position values
            Vector3 position = new(light.position.X, light.position.Y, light.position.Z);
            Raylib_CsLo.Raylib.SetShaderValue(shader, lightShaderPos.posLoc, position, ShaderUniformDataType.SHADER_UNIFORM_VEC3);

            // Send to shader light target position values
            Vector3 target = new(light.target.X, light.target.Y, light.target.Z);
            Raylib_CsLo.Raylib.SetShaderValue(shader, lightShaderPos.targetLoc, target, ShaderUniformDataType.SHADER_UNIFORM_VEC3);

            // Send to shader light color values
            Vector4 color = light.color;
            Raylib_CsLo.Raylib.SetShaderValue(shader, lightShaderPos.colorLoc, color, ShaderUniformDataType.SHADER_UNIFORM_VEC4);
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
        Raylib_CsLo.Raylib.SetShaderValue(
            ((RlShaderEntry)aShaderEntry).RlShader,
            _ambientLoc, colAmbient,
            ShaderUniformDataType.SHADER_UNIFORM_VEC4);
    }


    
    private unsafe void _createDefaultShader()
    {
        _rlInstanceShaderEntry = new RlShaderEntry();

        {
            byte[] byLightingInstancingVS = Encoding.ASCII.GetBytes(shadercode.LightingVS.ShaderCode);
            byte[] byLightingFS = Encoding.ASCII.GetBytes(shadercode.LightingFS.ShaderCode);
            fixed (byte* byLIVS = byLightingInstancingVS)
            {
                fixed (byte* byLFS = byLightingFS)
                {
                    _rlInstanceShaderEntry.RlShader = Raylib_CsLo.Raylib.LoadShaderFromMemory(
                        (sbyte*)byLIVS, (sbyte*)byLFS);
                }

            }
        }
        _rlInstanceShaderEntry.RlShader.locs[(int)ShaderLocationIndex.SHADER_LOC_MATRIX_MVP] =
            Raylib_CsLo.Raylib.GetShaderLocation(_rlInstanceShaderEntry.RlShader,  "mvp");
        _rlInstanceShaderEntry.RlShader.locs[(int)ShaderLocationIndex.SHADER_LOC_VECTOR_VIEW] =
            Raylib_CsLo.Raylib.GetShaderLocation(_rlInstanceShaderEntry.RlShader, "viewPos");
        _rlInstanceShaderEntry.RlShader.locs[(int)ShaderLocationIndex.SHADER_LOC_MATRIX_MODEL] =
            Raylib_CsLo.Raylib.GetShaderLocationAttrib(_rlInstanceShaderEntry.RlShader, "instanceTransform");
        _rlInstanceShaderEntry.RlShader.locs[(int)ShaderLocationIndex.SHADER_LOC_MATRIX_NORMAL] =
            Raylib_CsLo.Raylib.GetShaderLocationAttrib(_rlInstanceShaderEntry.RlShader, "matNormal");

        // Set default shader locations: attributes locations
        _rlInstanceShaderEntry.RlShader.locs[(int)ShaderLocationIndex.SHADER_LOC_VERTEX_POSITION] =
            Raylib_CsLo.Raylib.GetShaderLocation(_rlInstanceShaderEntry.RlShader, "vertexPosition");
        _rlInstanceShaderEntry.RlShader.locs[(int)ShaderLocationIndex.SHADER_LOC_VERTEX_TEXCOORD01] =
            Raylib_CsLo.Raylib.GetShaderLocation(_rlInstanceShaderEntry.RlShader, "vertexTexCoord");
        _rlInstanceShaderEntry.RlShader.locs[(int)ShaderLocationIndex.SHADER_LOC_VERTEX_COLOR] =
            Raylib_CsLo.Raylib.GetShaderLocation(_rlInstanceShaderEntry.RlShader, "vertexColor");

        // Set default shader locations: uniform locations
        // _rlInstanceShaderEntry.RlShader.locs[(int)ShaderLocationIndex.SHADER_LOC_MATRIX_MVP] =
        // Raylib_CsLo.Raylib.GetShaderLocation(_rlInstanceShaderEntry.RlShader, "mvp");
        _rlInstanceShaderEntry.RlShader.locs[(int)ShaderLocationIndex.SHADER_LOC_COLOR_DIFFUSE] =
            Raylib_CsLo.Raylib.GetShaderLocation(_rlInstanceShaderEntry.RlShader, "colDiffuse");
        _rlInstanceShaderEntry.RlShader.locs[(int)ShaderLocationIndex.SHADER_LOC_MAP_ALBEDO] =
            Raylib_CsLo.Raylib.GetShaderLocation(_rlInstanceShaderEntry.RlShader, "texture0");
        _rlInstanceShaderEntry.RlShader.locs[(int)ShaderLocationIndex.SHADER_LOC_MAP_NORMAL] =
            Raylib_CsLo.Raylib.GetShaderLocation(_rlInstanceShaderEntry.RlShader, "texture2");

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
        List<Matrix4x4> listTMatrices = new(nMatrices);
        for (int i = 0; i < nMatrices; ++i)
        {
            listTMatrices.Add(Matrix4x4.Transpose(spanMatrices[i]));
        }
        
#if NET6_0_OR_GREATER
        var spanRealMatrices = CollectionsMarshal.AsSpan<Matrix4x4>(listTMatrices);
#else
        Span<Matrix4x4> spanRealMatrices = meshItem.Value.Matrices.ToArray();
#endif

        Raylib_CsLo.Raylib.DrawMeshInstanced(
                ((RlMeshEntry)aMeshEntry).RlMesh,
                ((RlMaterialEntry)aMaterialEntry).RlMaterial,
                spanRealMatrices, // spanMatrices, 
                nMatrices
        );
    }   

    public unsafe void UploadMesh(in AMeshEntry aMeshEntry)
    {
        RlMeshEntry rlMeshEntry = (RlMeshEntry)aMeshEntry;
        fixed (Raylib_CsLo.Mesh* pRlMeshEntry = &rlMeshEntry.RlMesh)
        {
            Raylib_CsLo.Raylib.UploadMesh(pRlMeshEntry, false);
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
            Raylib_CsLo.Raylib.UnloadMesh(rlMeshEntry.RlMesh);
        });
    }

    public unsafe AMaterialEntry GetDefaultMaterial()
    {
        lock (_lo)
        {
            if (_loadingMaterial == null)
            {
                var loadingMaterial = new RlMaterialEntry(new engine.joyce.Material());

                Image checkedImage = Raylib_CsLo.Raylib.GenImageChecked(2, 2, 1, 1, Raylib_CsLo.Raylib.RED, Raylib_CsLo.Raylib.GREEN);
                var loadingTexture = Raylib_CsLo.Raylib.LoadTextureFromImage(checkedImage);
                Raylib_CsLo.Raylib.UnloadImage(checkedImage);

                loadingMaterial.RlMaterial = Raylib_CsLo.Raylib.LoadMaterialDefault();
                loadingMaterial.RlMaterial.shader = _rlInstanceShaderEntry.RlShader;
                loadingMaterial.RlMaterial.maps[(int)Raylib_CsLo.Raylib.MATERIAL_MAP_DIFFUSE].texture = loadingTexture;
                // loadingMaterial.RlMaterial.maps[(int)Raylib_CsLo.Raylib.MATERIAL_MAP_DIFFUSE].color = Raylib_CsLo.Raylib.WHITE;
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
        ATextureEntry aTextureEntry = null;

        if (jMaterial.Texture != null)
        {
            aTextureEntry = _textureManager.FindATexture(jMaterial.Texture);
            // TXWTODO: Add reference of this texture.
        }
        ATextureEntry aEmissiveTextureEntry = null;
        if (jMaterial.EmissiveTexture != null)
        {
            aEmissiveTextureEntry = _textureManager.FindATexture(jMaterial.EmissiveTexture);
        }
        else
        {
            aEmissiveTextureEntry = _textureManager.FindATexture(new engine.joyce.Texture("joyce://col00000000"));
        }

        rlMaterialEntry.RlMaterial = Raylib_CsLo.Raylib.LoadMaterialDefault();
        rlMaterialEntry.RlMaterial.shader = _rlInstanceShaderEntry.RlShader;
        if (null != aTextureEntry)
        {
            rlMaterialEntry.RlMaterial.maps[(int)MaterialMapIndex.MATERIAL_MAP_ALBEDO].texture =
                ((RlTextureEntry)aTextureEntry).RlTexture;
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
                ((RlTextureEntry)aEmissiveTextureEntry).RlTexture;
        }
    }


    public void UnloadMaterialEntry(in AMaterialEntry aMaterialEntry)
    {
        // TWTODO: Actually write this. 
    }


    public ATextureEntry CreateTextureEntry(in engine.joyce.Texture jTexture)
    {
        RlTextureEntry rlTextureEntry = new RlTextureEntry(jTexture);
        return rlTextureEntry;
    }


    public void FillTextureEntry(in Splash.ATextureEntry aTextureEntry)
    {
        _textureGenerator.FillTextureEntry(((RlTextureEntry)aTextureEntry));
    }
    
    
    public unsafe void SetCameraPos(in Vector3 vCamera)
    {
        Raylib_CsLo.Raylib.SetShaderValue(
            _rlInstanceShaderEntry.RlShader,
            _rlInstanceShaderEntry.RlShader.locs[(int)ShaderLocationIndex.SHADER_LOC_VECTOR_VIEW],
            vCamera,
            ShaderUniformDataType.SHADER_UNIFORM_VEC3);
    }

    public RaylibThreeD(in engine.Engine engine)
    {
        _engine = engine;
        _textureGenerator = new(engine);
        _createDefaultShader();
        GetDefaultMaterial();
        _textureManager = new TextureManager(this);
    }
   
}