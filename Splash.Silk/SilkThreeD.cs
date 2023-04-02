using System;
using System.Collections.Generic;
using System.Data;
using System.Numerics;
using System.Text;
using static engine.Logger;
using Silk.NET.OpenGL;


namespace Splash.Silk;


public class SilkThreeD : IThreeD
{
    private readonly engine.Engine _engine;
    private object _lo = new();
    
    private SkMaterialEntry _loadingMaterial;
    private readonly TextureGenerator _textureGenerator;
    private readonly TextureManager _textureManager;
    private GL _gl = null;

    private Matrix4x4 _matView;
    private Matrix4x4 _matProjection;

    private SkShaderEntry _skInstanceShaderEntry;
    
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
        in LightShaderPos lightShaderPos, int lightIndex, ref SkShader sh)
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

        lightShaderPos.enabledLoc = (int)sh.GetUniform(enabledName);
        lightShaderPos.typeLoc = (int)sh.GetUniform(typeName);
        lightShaderPos.posLoc = (int)sh.GetUniform(posName);
        lightShaderPos.targetLoc = (int)sh.GetUniform(targetName);
        lightShaderPos.colorLoc = (int)sh.GetUniform(colorName);

    }

    
    private LightShaderPos _getLightShaderPos(int index, ref SkShader sh)
    {
        lock (_lo)
        {
            if (null == _lightShaderPos)
            {
                _lightShaderPos = new LightShaderPos[LightManager.MAX_LIGHTS];
                for (int i = 0; i < LightManager.MAX_LIGHTS; ++i)
                {
                    _lightShaderPos[i] = new();
                    _compileLightLocked(_lightShaderPos[i], i, ref sh);
                }
                _ambientLoc = (int)sh.GetUniform("ambient");

            }

            return _lightShaderPos[index];
        }
    }
    
    
    /**
     * Update lights value in shader
     */
    private unsafe void _applyLightValues(ref SkShader sh, int index, in Light light)
    {
        var lightShaderPos = _getLightShaderPos(index, ref sh);
        bool checkLights = false;

        // Send to shader light enabled state and type
        sh.SetUniform(lightShaderPos.enabledLoc, (light.enabled?1:0));
        if( checkLights ) CheckError($"Set Uniform light enabled {index}");
        sh.SetUniform(lightShaderPos.typeLoc, (int)light.type);
        if( checkLights ) CheckError($"Set Uniform light type {index}");

        // Send to shader light position values
        Vector3 position = new(light.position.X, light.position.Y, light.position.Z);
        sh.SetUniform(lightShaderPos.posLoc, position);
        if( checkLights ) CheckError($"Set Uniform light position {index}");

        // Send to shader light target position values
        Vector3 target = new(light.target.X, light.target.Y, light.target.Z);
        sh.SetUniform(lightShaderPos.targetLoc, target);
        if( checkLights ) CheckError($"Set Uniform light targetLoc {index}");

        // Send to shader light color values
        Vector4 color = new((float)light.color.X / (float)255, (float)light.color.Y / (float)255,
            (float)light.color.Z / (float)255, (float)light.color.W / (float)255);
        sh.SetUniform(lightShaderPos.colorLoc, color);
        if( checkLights ) CheckError($"Set Uniform light color {index}");
    }


    private void _applyAllLights(in IList<Light> listLights, ref SkShader sh)
    {
        for (int i = 0; i < listLights.Count; i++)
        {
            _applyLightValues(ref sh, i, listLights[i]);                
        }
    }

    public void ApplyAllLights(in IList<Light> listLights, in AShaderEntry aShaderEntry)
    {
        var sh = ((SkShaderEntry)aShaderEntry).SkShader;
        if (null == sh)
        {
            return;
        }
        _applyAllLights(listLights, ref sh);
        CheckError( "applyAllLights");
    }
    
    public void ApplyAmbientLights(in Vector4 colAmbient, in AShaderEntry aShaderEntry)
    {
        var sh = ((SkShaderEntry)aShaderEntry).SkShader;
        bool checkLights = false;
        if (null == sh)
        {
            return;
        }
        sh.SetUniform(_ambientLoc, colAmbient);
        if( checkLights ) CheckError($"Set Uniform ambient light");
    }

    
    
    private void _createDefaultShader()
    {
        _skInstanceShaderEntry = new SkShaderEntry();
        _skInstanceShaderEntry.SkShader = new SkShader(
            _gl,
            shadercode.LightingVS.ShaderCode,
            shadercode.LightingFS.ShaderCode
        );

#if false
        _rlInstanceShaderEntry.RlShader.locs[(int)ShaderLocationIndex.SHADER_LOC_MATRIX_MVP] =
            Raylib_CsLo.Raylib.GetShaderLocation(_rlInstanceShaderEntry.RlShader, "mvp");
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
#endif
    }

    public SkShaderEntry GetInstanceShaderEntry()
    {
        lock (_lo)
        {
            if (null == _skInstanceShaderEntry)
            {
                _createDefaultShader();
            }
        }
        return _skInstanceShaderEntry;
    }

    private void _loadMaterialToShader(in SkShader sh, in SkMaterialEntry skMaterialEntry)
    {
        //sh.SetUniform("texture0");
        SkTextureEntry skDiffuseTextureEntry = skMaterialEntry.SkDiffuseTexture;
        if (skDiffuseTextureEntry != null && skDiffuseTextureEntry.IsUploaded())
        {
            SkTexture skTexture = skDiffuseTextureEntry.SkTexture;
            if (skTexture != null)
            {
                skTexture.Bind(TextureUnit.Texture0);
                CheckError("Bind Texture0");
            }
        }

        SkTextureEntry skEmissiveTextureEntry = skMaterialEntry.SkEmissiveTexture;
        if (skEmissiveTextureEntry != null && skEmissiveTextureEntry.IsUploaded())
        {
            SkTexture skTexture = skEmissiveTextureEntry.SkTexture;
            if (skTexture != null)
            {
                skTexture.Bind(TextureUnit.Texture2);
                CheckError("Bind Texture 2");
            }
        }

        sh.SetUniform("colDiffuse", new Vector4(skMaterialEntry.JMaterial.AlbedoColor));
        sh.SetUniform("ambient", new Vector4(.2f, .2f, .2f, 0.0f));
        sh.SetUniform("texture0", 0);
        sh.SetUniform("texture2", 2);
    }

    private void _unloadMaterialFromShader()
    {
        _gl.ActiveTexture(TextureUnit.Texture0);
        CheckError("unload AxtiveTexture0");
        _gl.BindTexture(TextureTarget.Texture2D, 0);
        _gl.ActiveTexture(TextureUnit.Texture2);
        CheckError("unload AxtiveTexture2");
        _gl.BindTexture(TextureTarget.Texture2D, 0);
    }


    private void _printUniforms()
    {
        GetInstanceShaderEntry();
        uint shaderHandle = _skInstanceShaderEntry.SkShader.Handle;
        string shaderLog = _gl.GetShaderInfoLog(shaderHandle);
        Console.WriteLine(shaderLog);
        for (int i = 0; i < 9; ++i)
        {
            int uniformSize;
            UniformType uniformType;
            string uniform = _gl.GetActiveUniform(shaderHandle, (uint)i, out uniformSize, out uniformType );
            Console.WriteLine( $"Active Uniform {i} size {uniformSize} type {uniformType}: uniform");
        }
        
    }

    public static bool UseInstanceRendering = false;

    public unsafe void DrawMeshInstanced(
        in AMeshEntry aMeshEntry,
        in AMaterialEntry aMaterialEntry,
        in Span<Matrix4x4> spanMatrices,
        in int nMatrices)
    {
        CheckError("Beginning of DrawMeshInstanced");
        SkMeshEntry skMeshEntry = ((SkMeshEntry)aMeshEntry);
        //VertexArrayObject skMesh = skMeshEntry.vao;

        SkMaterialEntry skMaterialEntry = ((SkMaterialEntry)aMaterialEntry);

        /*
         * 1. set shader uniforms if the material has changed
         * 2. Actually draw mesh.
         */
        GetInstanceShaderEntry();
        SkShader sh = _skInstanceShaderEntry.SkShader;
        sh.Use();

        /*
         * Load the material, if it changed since the last
         * call. Usually it does because we already group
         * calls by material.
         */
        _loadMaterialToShader(sh, (SkMaterialEntry)aMaterialEntry);

        /*
         * Load the mesh, if it changed since the last call.
         */
        if (!skMeshEntry.IsMeshUploaded())
        {
            skMeshEntry.Upload(_gl);
        }

        /*
         * 1) Bind the vao and
         * 2) upload the matrix instance buffer.
         */

        BufferObject<Matrix4x4> bMatrices = null;

        if (UseInstanceRendering)
        {
            skMeshEntry.vao.BindVertexArray();
            CheckError("Bind Vertex Array");
            bMatrices = new BufferObject<Matrix4x4>(_gl, spanMatrices, BufferTargetARB.ArrayBuffer);
            CheckError("New Buffer Object");
            bMatrices.BindBuffer();
            CheckError("Bind Buffer");
            uint locInstanceMatrices = sh.GetAttrib("instanceTransform");
            for (uint i = 0; i < 4; ++i)
            {
                _gl.EnableVertexAttribArray(locInstanceMatrices + i);
                CheckError("Enable vertex array in instances");
                _gl.VertexAttribPointer(
                    locInstanceMatrices + i,
                    4,
                    VertexAttribPointerType.Float,
                    false,
                    16 * (uint)sizeof(float),
                    (void*)(sizeof(float) * i * 4)
                );
                CheckError("Enable vertex attribut pointer n");
                _gl.VertexAttribDivisor(locInstanceMatrices + i, 1);
                CheckError("attrib divisor");
            }

            /*
             * Disable buffers again.
             */
            _gl.BindVertexArray(0);
            _gl.BindBuffer(GLEnum.ArrayBuffer, 0);
        }
        
        /*
         * Setup view and projection matrix.
         * We need a combined view and projection matrix
         */

        skMeshEntry.vao.BindVertexArray();
        CheckError("instance vertex array bind");

        // Matrix4x4 matTotal = mvp * Matrix4x4.Transpose(spanMatrices[0]);
        // Vector4 v0 = Vector4.Transform(new Vector4( skMeshEntry.JMesh.Vertices[0], 0f), matTotal);
        if (UseInstanceRendering) 
        {
            Matrix4x4 mvp = Matrix4x4.Transpose(_matView * _matProjection);
            sh.SetUniform("mvp", mvp);
            CheckError("upload mvp");
            _gl.DrawElementsInstanced(
                PrimitiveType.Triangles,
                (uint)skMeshEntry.JMesh.Indices.Count,
                GLEnum.UnsignedShort,
                (void*)0,
                (uint)nMatrices);
            CheckError("draw instance");
        }
        else
        {
            for (int i = 0; i < nMatrices; ++i)
            {
                Matrix4x4 mvpi = Matrix4x4.Transpose(Matrix4x4.Transpose(spanMatrices[i]) * _matView * _matProjection);
                sh.SetUniform("mvp", mvpi);
                CheckError("upload mvpi");
                _gl.DrawElements(
                    PrimitiveType.Triangles,
                    (uint)skMeshEntry.JMesh.Indices.Count,
                    DrawElementsType.UnsignedShort,
                    (void*)0);
                CheckError("draw elements");
            }
        }
        
        _unloadMaterialFromShader();
        _gl.BindVertexArray(0);
        _gl.BindBuffer( GLEnum.ArrayBuffer, 0);
        _gl.BindBuffer( GLEnum.ElementArrayBuffer, 0);
        
        // TXWTODO: Shall we really always disable the current program?
        _gl.UseProgram(0);

        if (null != bMatrices)
        {
            bMatrices.Dispose();
        }

    }   

    public void UploadMesh(in AMeshEntry aMeshEntry)
    {
        SkMeshEntry skMeshEntry = ((SkMeshEntry)aMeshEntry);
        if (!skMeshEntry.IsMeshUploaded())
        {
            skMeshEntry.Upload(_gl);
        }
        //Trace($"Uploaded Mesh vaoId={rlMeshEntry.RlMesh.vaoId}, nVertices={rlMeshEntry.RlMesh.vertexCount}");
    }

    public AMeshEntry CreateMeshEntry(in engine.joyce.Mesh jMesh)
    {
        SkMeshEntry skMeshEntry;
        // TXWTODO: Also async API?
        MeshGenerator.CreateSilkMesh(jMesh, out skMeshEntry);
        return skMeshEntry;
    }

    public void DestroyMeshEntry(in AMeshEntry aMeshEntry)
    {
        SkMeshEntry rlMeshEntry = (SkMeshEntry)aMeshEntry;
        _engine.QueueCleanupAction(() =>
        {
            //Trace($"Unloading Mesh vaoId={rlMeshEntry.RlMesh.vaoId}, nVertices={rlMeshEntry.RlMesh.vertexCount}");
            //Raylib_CsLo.Raylib.UnloadMesh(rlMeshEntry.RlMesh);
        });
    }

    public AMaterialEntry GetDefaultMaterial()
    {
        lock (_lo)
        {
            if (_loadingMaterial == null)
            {
#if false
                var loadingMaterial = new RlMaterialEntry(new engine.joyce.Material());

                Image checkedImage = Raylib_CsLo.Raylib.GenImageChecked(2, 2, 1, 1, Raylib_CsLo.Raylib.RED, Raylib_CsLo.Raylib.GREEN);
                var loadingTexture = Raylib_CsLo.Raylib.LoadTextureFromImage(checkedImage);
                Raylib_CsLo.Raylib.UnloadImage(checkedImage);

                loadingMaterial.RlMaterial = Raylib_CsLo.Raylib.LoadMaterialDefault();
                loadingMaterial.RlMaterial.shader = _rlInstanceShaderEntry.RlShader;
                loadingMaterial.RlMaterial.maps[(int)Raylib_CsLo.Raylib.MATERIAL_MAP_DIFFUSE].texture = loadingTexture;
                // loadingMaterial.RlMaterial.maps[(int)Raylib_CsLo.Raylib.MATERIAL_MAP_DIFFUSE].color = Raylib_CsLo.Raylib.WHITE;
                _loadingMaterial = loadingMaterial;
#endif
            }

            return _loadingMaterial;
        }
    }

    public AMaterialEntry CreateMaterialEntry(in engine.joyce.Material jMaterial)
    {
        SkMaterialEntry skMaterialEntry = new SkMaterialEntry(jMaterial);
        return skMaterialEntry;
    }


    public void FillMaterialEntry(in AMaterialEntry aMaterialEntry)
    {
        SkMaterialEntry skMaterialEntry = (SkMaterialEntry) aMaterialEntry;
        engine.joyce.Material jMaterial = skMaterialEntry.JMaterial;
        ATextureEntry aTextureEntry = null;

        if (jMaterial.Texture != null && jMaterial.Texture.Source != null && jMaterial.Texture.Source != "")
        {
            aTextureEntry = _textureManager.FindATexture(jMaterial.Texture);
            skMaterialEntry.SkDiffuseTexture = ((SkTextureEntry)aTextureEntry);
        }
        ATextureEntry aEmissiveTextureEntry = null;
        if (jMaterial.EmissiveTexture != null && jMaterial.EmissiveTexture.Source != null && jMaterial.EmissiveTexture.Source != "")
        {
            aEmissiveTextureEntry = _textureManager.FindATexture(jMaterial.EmissiveTexture);
            skMaterialEntry.SkEmissiveTexture = ((SkTextureEntry)aEmissiveTextureEntry);
        }
        else
        {
            aEmissiveTextureEntry = _textureManager.FindATexture(new engine.joyce.Texture("joyce://col00000000"));
            skMaterialEntry.SkEmissiveTexture = ((SkTextureEntry)aEmissiveTextureEntry);
        }

        skMaterialEntry.SetUploaded();
#if false
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
#endif

    }


    public void UnloadMaterialEntry(in AMaterialEntry aMaterialEntry)
    {
        // TWTODO: Actually write this. 
    }


    public ATextureEntry CreateTextureEntry(in engine.joyce.Texture jTexture)
    {
        SkTextureEntry skTextureEntry = new SkTextureEntry(jTexture);
        return skTextureEntry;
    }


    public void FillTextureEntry(in Splash.ATextureEntry aTextureEntry)
    {
        _textureGenerator.FillTextureEntry(((SkTextureEntry)aTextureEntry));
    }
    
    
    public void SetCameraPos(in Vector3 vCamera)
    {
        /*
         * Push the viewer's position to the fragment shader
         */
        // TXWTODO: Write me.

        /* Raylib_CsLo.Raylib.SetShaderValue(
            _rlInstanceShaderEntry.RlShader,
            _rlInstanceShaderEntry.RlShader.locs[(int)ShaderLocationIndex.SHADER_LOC_VECTOR_VIEW],
            vCamera,
            ShaderUniformDataType.SHADER_UNIFORM_VEC3);
            */
    }


    /**
     * Set the current view matrix, transforming object space to camera.
     * @param matView
     *    view projection matrix, .NET order.
     */
    public void SetViewMatrix(in Matrix4x4 matView)
    {
        _matView = matView;
    }


    /**
     * Set the current projection matrix
     * @param matProjection
     *    perspective projection matrix, .NET order.
     */
    public void SetProjectionMatrix(in Matrix4x4 matProjection)
    {
        _matProjection = matProjection;
    }

    public void CheckError(string what)
    {
        var error = _gl.GetError();
        if (error != GLEnum.NoError)
        {
            Error( $"Found OpenGL {what} error {error}" );
            // ErrorThrow( $"{name}: Found OpenGL error {error}", m => new InvalidOperationException(m));
        }
        else
        {
            Console.WriteLine($"OK: {what}");
        }
    }

    public void SetGL(in GL gl)
    {
        _gl = gl;
        
        _gl.Enable(EnableCap.CullFace);
        _gl.CullFace(CullFaceMode.Back);
        _gl.FrontFace(FrontFaceDirection.Ccw);
        _gl.Enable(EnableCap.DebugOutput);
        _gl.Disable(EnableCap.DebugOutputSynchronous);
        _gl.Enable(EnableCap.DepthClamp);
        _gl.Enable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.ScissorTest);
        _gl.Disable(EnableCap.StencilTest);
    }

    public GL GetGL()
    {
        return _gl;
    }

    public SilkThreeD(in engine.Engine engine)
    {
        _engine = engine;
        _textureGenerator = new(engine, this);
        //_createDefaultShader();
        GetDefaultMaterial();
        _textureManager = new TextureManager(this);
    }
   
}