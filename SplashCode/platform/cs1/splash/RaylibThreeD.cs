using System;
using System.Numerics;
using System.Text;
using Raylib_CsLo;
using static engine.Logger;

namespace Karawan.platform.cs1.splash;

public class RaylibThreeD : IThreeD, IRenderer
{
    private readonly engine.Engine _engine;
    private object _lo = new();
    
    private RlMaterialEntry _loadingMaterial; 
    private readonly TextureManager _textureManager;

    private RlShaderEntry _rlInstanceShaderEntry;
    
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