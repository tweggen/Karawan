using System;
using System.Collections.Generic;
using System.Data;
using System.Numerics;
using System.Text;
using engine;
using engine.joyce;
using static engine.Logger;
using Silk.NET.OpenGL;
using static Splash.Silk.GLCheck;

namespace Splash.Silk;


public class SilkThreeD : IThreeD
{
    private readonly engine.Engine _engine;
    
    public engine.Engine Engine
    {
        get => _engine;
    }
    
    private object _lo = new();
    
    private SkMaterialEntry? _loadingMaterial = null;
    private TextureGenerator _textureGenerator;
    private TextureManager _textureManager;
    private ShaderManager _shaderManager;
    private GL? _gl = null;

    /*
     * Sort of shader parameters. Where to?
     */
    private Matrix4x4 _matView;
    private Matrix4x4 _matProjection;
    private Vector3 _vCamera;
    private float _fogDistance;
    private Vector3 _v3FogColor = new(0.2f, 0.18f, 0.2f); 

    private int _nUploadedMeshes = 0;
    
    private RenderFrame _currentRenderFrame = null;
    private LightCollector _currentLightCollector = null;
    
    private readonly engine.WorkerQueue _graphicsThreadActions = new("Splash.silk.graphicsThreadActions");

    private List<IShaderUseCase> _listShaderUseCases = new();
    

    private GL _getGL()
    {
        if (null == _gl)
        {
            ErrorThrow("_gl is null.", (m)=>new InvalidOperationException(m));
            throw new InvalidOperationException("goo");
        }

        return _gl;
    }


    private SkMaterialEntry _lastMaterialEntry = null;
    
    
    /**
     * Assuming, the current program already is loaded, apply setting the uniforms and channels 
     */
    private void _loadMaterialToShader(in SkProgramEntry sh, in SkMaterialEntry skMaterialEntry)
    {
        /*
         * Perform peephole optimization to load only if necessary.
         * Only really meaningful if the draw calls are sorted.
         */
        {
            if (_lastMaterialEntry != skMaterialEntry)
            {
                _unloadMaterialFromShader();
            }

            _useProgramEntry(sh);

            if (_lastMaterialEntry == skMaterialEntry)
            {
                return;
            }

            _lastMaterialEntry = skMaterialEntry;
        }

        try
        {
            SkTextureEntry? skDiffuseTextureEntry = skMaterialEntry.SkDiffuseTexture;
            if (skDiffuseTextureEntry != null && skDiffuseTextureEntry.IsUploaded())
            {
                SkTexture? skTexture = skDiffuseTextureEntry.SkTexture;
                if (skTexture != null)
                {
                    skTexture.ActiveAndBind(TextureUnit.Texture0);
                }
            }

            SkTextureEntry? skEmissiveTextureEntry = skMaterialEntry.SkEmissiveTexture;
            if (skEmissiveTextureEntry != null && skEmissiveTextureEntry.IsUploaded())
            {
                SkTexture? skTexture = skEmissiveTextureEntry.SkTexture;
                if (skTexture != null)
                {
                    skTexture.ActiveAndBind(TextureUnit.Texture2);
                }
            }

            engine.joyce.Material jMaterial = skMaterialEntry.JMaterial;
            sh.SetUniform("col4Diffuse", new Vector4(
                ((jMaterial.AlbedoColor >> 16) & 0xff) / 255f,
                ((jMaterial.AlbedoColor >> 8) & 0xff) / 255f,
                ((jMaterial.AlbedoColor) & 0xff) / 255f,
                ((jMaterial.AlbedoColor >> 24) & 0xff) / 255f
            ));
            sh.SetUniform("col4Emissive", new Vector4(
                ((jMaterial.EmissiveColor >> 16) & 0xff) / 255f,
                ((jMaterial.EmissiveColor >> 8) & 0xff) / 255f,
                ((jMaterial.EmissiveColor) & 0xff) / 255f,
                ((jMaterial.EmissiveColor >> 24) & 0xff) / 255f
            ));
            sh.SetUniform("col4EmissiveFactors", new Vector4(
                ((jMaterial.EmissiveFactors >> 16) & 0xff) / 255f,
                ((jMaterial.EmissiveFactors >> 8) & 0xff) / 255f,
                ((jMaterial.EmissiveFactors) & 0xff) / 255f,
                ((jMaterial.EmissiveFactors >> 24) & 0xff) / 255f
            ));
            
            // sh.SetUniform("ambient", new Vector4(.2f, .2f, .2f, 0.0f));
            sh.SetUniform("texture0", 0);
            sh.SetUniform("texture2", 2);

            Material.ShaderFlags materialFlags = 0;
            if (jMaterial.AddInterior)
            {
                materialFlags |= Material.ShaderFlags.RenderInterior;
            }
            sh.SetUniform("materialFlags", (int) materialFlags);
        }
        catch (Exception e)
        {
            Error($"Error loading material to shader: {e}");
        }
    }

    
    /**
     * Unload the material specifics from the shader.
     */
    private void _unloadMaterialFromShader()
    {
        var skMaterialEntry = _lastMaterialEntry;
        if (null == skMaterialEntry) return;
        
        _lastMaterialEntry = null;
        try
        {
            //sh.SetUniform("texture0");
            SkTextureEntry? skDiffuseTextureEntry = skMaterialEntry.SkDiffuseTexture;
            if (skDiffuseTextureEntry != null && skDiffuseTextureEntry.IsUploaded())
            {
                SkTexture? skTexture = skDiffuseTextureEntry.SkTexture;
                if (skTexture != null)
                {
                    skTexture.ActiveAndUnbind(TextureUnit.Texture0);
                }
            }

            SkTextureEntry? skEmissiveTextureEntry = skMaterialEntry.SkEmissiveTexture;
            if (skEmissiveTextureEntry != null && skEmissiveTextureEntry.IsUploaded())
            {
                SkTexture? skTexture = skEmissiveTextureEntry.SkTexture;
                if (skTexture != null)
                {
                    skTexture.ActiveAndUnbind(TextureUnit.Texture2);
                }
            }

        }
        catch (Exception e)
        {
            Error($"Error loading material to shader: {e}");
        }
    }


    public void LoadFrame(RenderFrame renderFrame)
    {
        _currentRenderFrame = renderFrame;
    }
    
    
    /*
     * Detach any pending programs from the pipeline.
     */
    public void UnloadAfterFrame()
    {
        if (null != _lastMaterialEntry)
        {
            _unloadMaterialFromShader();
        }

        if (null != _lastProgramEntry)
        {
            _unloadProgramEntry();
        }

        _currentRenderFrame = null;
        _frameno++;
    }

    private static readonly  bool _useInstanceRendering = true;

    public void FinishUploadOnly(in AMeshEntry aMeshEntry)
    {
        // TXWTODO: Some of these calls have been required.
        // SkMeshEntry skMeshEntry = ((SkMeshEntry)aMeshEntry);
        // skMeshEntry.vao.BindVertexArray();
        // _gl.BindVertexArray(0);
        // _gl.BindBuffer(GLEnum.ArrayBuffer, 0);

    }


    private SkProgramEntry _lastProgramEntry = null;

    private void _useProgramEntry(SkProgramEntry sh)
    {
        if (_lastProgramEntry == sh)
        {
            return;
        }

        _lastProgramEntry = sh;
        sh.Use();
        _setupProgramGlobals(sh);
    }


    private void _unloadProgramEntry()
    {
        if (null == _lastProgramEntry)
        {
            return;
        }

        var pe = _lastProgramEntry;
        _lastProgramEntry = null;
        _gl.UseProgram(pe.Handle);
    }
    
    
    public unsafe void DrawMeshInstanced(
        in AMeshEntry aMeshEntry,
        in AMaterialEntry aMaterialEntry,
        in Span<Matrix4x4> spanMatrices,
        in int nMatrices)
    {
        var gl = _getGL();
        
        CheckError(gl,"Beginning of DrawMeshInstanced");
        SkMeshEntry skMeshEntry = ((SkMeshEntry)aMeshEntry);
        //VertexArrayObject skMesh = skMeshEntry.vao;

        SkMaterialEntry skMaterialEntry = ((SkMaterialEntry)aMaterialEntry);

        /*
         * 1. set shader uniforms if the material has changed
         * 2. Actually draw mesh.
         */
        SkProgramEntry sh = skMaterialEntry.SkProgram;
        
        /*
         * Use the program and load program globals.
         * Load the material, if it changed since the last
         * call. Usually it does because we already group
         * calls by material.
         */
        _loadMaterialToShader(sh, skMaterialEntry);

        /*
         * Load the mesh, if it changed since the last call.
         */
        if (!skMeshEntry.IsUploaded())
        {
            Error("Mesh should have been uploaded by now.");
            // skMeshEntry.Upload(gl);
            return;
        }

        /*
         * 1) Bind the vao and
         * 2) upload the matrix instance buffer.
         */

        // TXWTODO: Only re-bind it if it has changed since the last call.
        BufferObject<Matrix4x4>? bMatrices = null;

        if (_useInstanceRendering)
        {
            skMeshEntry.vao.BindVertexArray();
            CheckError(gl,"Bind Vertex Array");
            bMatrices = new BufferObject<Matrix4x4>(_gl, spanMatrices, BufferTargetARB.ArrayBuffer);
            CheckError(gl,"New Buffer Object");
            bMatrices.BindBuffer();
            CheckError(gl,"Bind Buffer");
            uint locInstanceMatrices = sh.GetAttrib("instanceTransform");
            for (uint i = 0; i < 4; ++i)
            {
                gl.EnableVertexAttribArray(locInstanceMatrices + i);
                CheckError(gl,"Enable vertex array in instances");
                gl.VertexAttribPointer(
                    locInstanceMatrices + i,
                    4,
                    VertexAttribPointerType.Float,
                    false,
                    16 * (uint)sizeof(float),
                    (void*)(sizeof(float) * i * 4)
                );
                CheckError(gl,"Enable vertex attribute pointer n");
                gl.VertexAttribDivisor(locInstanceMatrices + i, 1);
                CheckError(gl,"attrib divisor");
            }

            /*
             * Disable buffers again.
             * TXWTODO: Why that????
             */
            // gl.BindVertexArray(0);
            // gl.BindBuffer(GLEnum.ElementArrayBuffer, 0);
        }
        else
        {
            skMeshEntry.vao.BindVertexArray();
            CheckError(gl,"instance vertex array bind");
        }
        
        /*
         * Setup view and projection matrix.
         * We need a combined view and projection matrix
         */

        var jMesh = skMeshEntry.Params.JMesh;
        // Matrix4x4 matTotal = mvp * Matrix4x4.Transpose(spanMatrices[0]);
        // Vector4 v0 = Vector4.Transform(new Vector4( skMeshEntry.JMesh.Vertices[0], 0f), matTotal);
        if (_useInstanceRendering) 
        {
            Matrix4x4 mvp = _matView * _matProjection;
            sh.SetUniform("mvp", mvp);
            if (jMesh.Vertices.Count > 65535)
            {
                Error($"Trying to render mesh {skMeshEntry.vao.Handle} with too much mesh vertices at once ({jMesh.Vertices.Count})");
            }
            if (jMesh.Indices.Count > 65535)
            {
                Error($"Trying to render mesh {skMeshEntry.vao.Handle} with too much mesh vertices at once ({jMesh.Indices.Count})");
            }
            if (nMatrices > 1023)
            {
                Error($"Trying to render mesh {skMeshEntry.vao.Handle} with too much mesh instances at once ({nMatrices})");
            }
            gl.DrawElementsInstanced(
                PrimitiveType.Triangles,
                (uint)jMesh.Indices.Count,
                GLEnum.UnsignedShort,
                (void*)0,
                (uint)nMatrices);
            CheckError(gl,"draw elements instanced");
        }
        else
        {
            if (jMesh.Vertices.Count > 65535)
            {
                Error($"Trying to render mesh {skMeshEntry.vao.Handle} with too much mesh vertices at once ({jMesh.Vertices.Count})");
            }
            if (jMesh.Indices.Count > 65535)
            {
                Error($"Trying to render mesh {skMeshEntry.vao.Handle} with too much mesh vertices at once ({jMesh.Indices.Count})");
            }

            for (int i = 0; i < nMatrices; ++i)
            {
                Matrix4x4 mvpi = Matrix4x4.Transpose(spanMatrices[i]) * _matView * _matProjection;
                sh.SetUniform("mvp", mvpi);
                CheckError(gl,"upload mvpi");
                gl.DrawElements(
                    PrimitiveType.Triangles,
                    (uint)jMesh.Indices.Count,
                    DrawElementsType.UnsignedShort,
                    (void*)0);
                CheckError(gl,"draw elements");
            }
        }
        
        gl.BindVertexArray(0);
        gl.BindBuffer( GLEnum.ArrayBuffer, 0);
        gl.BindBuffer( GLEnum.ElementArrayBuffer, 0);
        
        if (null != bMatrices)
        {
            bMatrices.Dispose();
        }

    }   
    

    public void UploadMeshEntry(in AMeshEntry aMeshEntry)
    {
        var gl = _getGL();
        SkMeshEntry skMeshEntry = ((SkMeshEntry)aMeshEntry);
        if (!skMeshEntry.IsUploaded())
        {
            skMeshEntry.Upload();
            if (CheckError(gl,"AfterUpload mesh") < 0)
            {
                Trace("Something to break here.");
            }
            ++_nUploadedMeshes;
        }
    }

    
    /**
     * Create a silk mesh entry for the given mesh. That is converting
     * engine representation to silk representation, but not yet uploading it.
     */
    public AMeshEntry CreateMeshEntry(in AMeshParams aMeshParams)
    {
        var skMeshEntry = new SkMeshEntry(_getGL(), aMeshParams);
        return skMeshEntry;
    }
    

    public void FillMeshEntry(in AMeshEntry aMeshEntry)
    {
        MeshGenerator.FillSilkMesh(aMeshEntry as SkMeshEntry);
    }

    
    public void UnloadMeshEntry(in AMeshEntry aMeshEntry)
    {
        SkMeshEntry skMeshEntry = (SkMeshEntry)aMeshEntry;
        _graphicsThreadActions.Enqueue(() =>
        {
            int nUploadedMeshes;
            if (skMeshEntry.IsUploaded())
            {
                skMeshEntry.Release();
                nUploadedMeshes = --_nUploadedMeshes;
                // Trace($"Only {nUploadedMeshes} uploaded right now.");
            }
        });
    }

    
    public AMaterialEntry GetDefaultMaterial()
    {
        lock (_lo)
        {
            if (_loadingMaterial == null)
            {
                throw new InvalidOperationException("not yet implemented");
            }

            return _loadingMaterial;
        }
    }
    

    public AMaterialEntry CreateMaterialEntry(in engine.joyce.Material jMaterial)
    {
        SkMaterialEntry skMaterialEntry = new SkMaterialEntry(jMaterial);
        return skMaterialEntry;
    }


    private int _frameno;


    private SkSingleShaderEntry _compileSingleShader(SplashAnyShader splashAnyShader, ShaderType shaderType)
    {
        return new SkSingleShaderEntry(_getGL(), splashAnyShader, shaderType);
    }
    

    /**
     * Note that fill material entry also is called if the material already had been uplodaded but is outdated.
     * Therefore we need to test which of the resources needs to be created and which needs to be updated only.
     */
    public void FillMaterialEntry(in AMaterialEntry aMaterialEntry)
    {
        SkMaterialEntry skMaterialEntry = (SkMaterialEntry) aMaterialEntry;
        bool haveUploadSuccess = true;
        
        engine.joyce.Material jMaterial = skMaterialEntry.JMaterial;

        {
            if (null == skMaterialEntry.SkFragmentShader)
            {
                string fragmentShaderName = jMaterial.FragmentShader;
                if (String.IsNullOrEmpty(fragmentShaderName))
                {
                    fragmentShaderName = "shaders/default.frag";
                }

                engine.Resource.ShaderSource? fragmentShaderSource =
                    (I.Get<Resources>().Get(fragmentShaderName)) as engine.Resource.ShaderSource;
                if (fragmentShaderSource == null)
                {
                    ErrorThrow("Internal error: Even the default fragment shader is not valid.",
                        m => new InvalidOperationException(m));
                    return;
                }

                engine.joyce.AnyShader? fragmentShader = new SplashAnyShader()
                    { Source = fragmentShaderSource.ShaderCode };
                ASingleShaderEntry? aFragmentShaderEntry = _shaderManager.FindAdd(
                    fragmentShader,
                    (anyShader) => new SkSingleShaderEntry(
                        _getGL(), anyShader as SplashAnyShader, ShaderType.FragmentShader));

                skMaterialEntry.SkFragmentShader = ((SkSingleShaderEntry)aFragmentShaderEntry);
            }

            if (null == skMaterialEntry.SkVertexShader)
            {
                string vertexShaderName = jMaterial.VertexShader;
                if (String.IsNullOrEmpty(vertexShaderName))
                {
                    vertexShaderName = "shaders/default.vert";
                }

                engine.Resource.ShaderSource? vertexShaderSource =
                    (I.Get<Resources>().Get(vertexShaderName)) as engine.Resource.ShaderSource;
                if (vertexShaderSource == null)
                {
                    ErrorThrow("Internal error: Even the default vertex shader is not valid.",
                        m => new InvalidOperationException(m));
                    return;
                }

                engine.joyce.AnyShader? vertexShader = new SplashAnyShader() { Source = vertexShaderSource.ShaderCode };
                ASingleShaderEntry? aVertexShaderEntry = _shaderManager.FindAdd(
                    vertexShader,
                    (anyShader) => new SkSingleShaderEntry(
                        _getGL(), anyShader as SplashAnyShader, ShaderType.VertexShader));
                
                skMaterialEntry.SkVertexShader = ((SkSingleShaderEntry)aVertexShaderEntry);
            }

            if (null == skMaterialEntry.SkProgram)
            {
                skMaterialEntry.SkProgram = new SkProgramEntry(_gl,
                    skMaterialEntry.SkVertexShader, skMaterialEntry.SkFragmentShader);
            }

            /*
             * Note, that the program shader uploads the vertex shader and the fragment shader.
             */
            if (!skMaterialEntry.SkProgram.IsUploaded()) skMaterialEntry.SkProgram.Upload();
        }

        if (jMaterial.Texture != null && jMaterial.Texture.IsValid())
        {
            ATextureEntry? aTextureEntry = _textureManager.FindATexture(jMaterial.Texture);
            if (null != aTextureEntry)
            {
                skMaterialEntry.SkDiffuseTexture = ((SkTextureEntry)aTextureEntry);
            }
            else
            {
                Warning($"Unable to uploade texture {jMaterial.Texture.Key}");
                haveUploadSuccess = false;
            }
        }
        else
        {
            ATextureEntry? aTextureEntry = _textureManager.FindATexture(new engine.joyce.Texture("joyce://col00000000"));
            skMaterialEntry.SkDiffuseTexture = ((SkTextureEntry)aTextureEntry);
        }
        if (jMaterial.EmissiveTexture != null && jMaterial.EmissiveTexture.IsValid())
        {
            ATextureEntry? aEmissiveTextureEntry = _textureManager.FindATexture(jMaterial.EmissiveTexture);
            if (null != aEmissiveTextureEntry)
            {
                skMaterialEntry.SkEmissiveTexture = ((SkTextureEntry)aEmissiveTextureEntry);
            }
            else
            {
                Warning($"Unable to uploade texture {jMaterial.EmissiveTexture.Key}");
                haveUploadSuccess = false;
            }
        }
        else
        {
            ATextureEntry? aEmissiveTextureEntry = _textureManager.FindATexture(new engine.joyce.Texture("joyce://col00000000"));
            skMaterialEntry.SkEmissiveTexture = ((SkTextureEntry)aEmissiveTextureEntry);
        }

        if (haveUploadSuccess)
        {
            skMaterialEntry.SetUploaded();
        }
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


    /**
     * Prepare all data for this texture that would be required for upload.
     * (This is not necessarily binary data, however the data should be
     * available on very short notice).
     *
     * This one performs the lookup from the texture tag to the real texture
     * uri including the UVSCale.
     *
     * While the texture atlas implementation is generic, the use of it is
     * specific to Splash (but not SplashSilk)
     */
    public void FillTextureEntry(in Splash.ATextureEntry aTextureEntry)
    {
        
    }
    
    
    /**
     * Associate the texture entry with the platform texture buffer,
     * uploading the content if required on the GPU.
     */
    public void UploadTextureEntry(in Splash.ATextureEntry aTextureEntry)
    {
        _textureGenerator.LoadUploadTextureEntry(((SkTextureEntry)aTextureEntry));
    }


    private void _resolveProgramUseCases(SkProgramEntry shader)
    {
        foreach (var usecase in _listShaderUseCases)
        {
            if (!shader.ShaderUseCases.ContainsKey(usecase.Name))
            {
                shader.ShaderUseCases[usecase.Name] = usecase.Compile(shader);
            }
        }
    }


    private void _setupProgramGlobals(SkProgramEntry shader)
    {
        /*
         * Before using the shader at all, make sure all our use cases are
         * resolved
         */
        _resolveProgramUseCases(shader);
        
        /*
         * Now specific calls.
         * FIXME: This needs a more beautiful API.
         */
        {
            LightShaderUseCaseLocs uc = 
                shader.ShaderUseCases[LightShaderUseCase.StaticName]
                as LightShaderUseCaseLocs;
            uc.Apply(_getGL(), shader, _currentRenderFrame.LightCollector);
        }
        shader.SetUniform("fogDistance", _fogDistance);
        shader.SetUniform("col3Fog", _v3FogColor);

        shader.SetUniform("v3AbsPosView", _vCamera);
        shader.SetUniform("frameNo", _frameno);
    }
    
    
    public void SetCameraPos(in Vector3 vCamera)
    {
        _vCamera = vCamera;
    }


    public void SetFogDistance(float fogDistance)
    {
        _fogDistance = fogDistance;
    }


    public void SetFogColor(Vector3 fogColor)
    {
        _v3FogColor = fogColor;
    }

    
    public ARenderbufferEntry CreateRenderbuffer(in engine.joyce.Renderbuffer jRenderbuffer)
    {
        SkRenderbufferEntry skRenderbufferEntry = new SkRenderbufferEntry(jRenderbuffer);
        return skRenderbufferEntry;
    }
    
    public void UploadRenderbuffer(in ARenderbufferEntry aRenderbufferEntry)
    {
        SkRenderbufferEntry skRenderbufferEntry = ((SkRenderbufferEntry)aRenderbufferEntry);
        if (!skRenderbufferEntry.IsUploaded())
        {
            skRenderbufferEntry.Upload(_getGL(), _textureManager);
        }

    }

    
    public void UnloadRenderbuffer(in ARenderbufferEntry aRenderbufferEntry)
    {
        SkRenderbufferEntry skRenderbufferEntry = (SkRenderbufferEntry)aRenderbufferEntry;
        _graphicsThreadActions.Enqueue(() =>
        {
            if (skRenderbufferEntry.IsUploaded())
            {
                skRenderbufferEntry.Release(_getGL());
            } 
        });
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

    
    public void SetGL(in GL gl)
    {
        _gl = gl;

#if false 
//requires GL4
        {
            _gl.GetInternalformat(GLEnum.Texture0, GLEnum.Rgba,
                GLEnum.InternalformatPreferred,
                1, out long value);
            Trace($"Preferred format is {value}.");
        }
#endif
        
        _gl.Enable(EnableCap.CullFace);
        _gl.CullFace(TriangleFace.Back);
        _gl.FrontFace(FrontFaceDirection.Ccw);
        _gl.Enable(EnableCap.DebugOutput);
        _gl.Disable(EnableCap.DebugOutputSynchronous);
        _gl.Enable(EnableCap.DepthClamp);
        _gl.Enable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.ScissorTest);
        _gl.Disable(EnableCap.StencilTest);

        _gl.GetInteger(GetPName.MaxElementsVertices, out var maxVertices);
        _gl.GetInteger(GetPName.MaxElementsIndices, out var maxIndices);
        _gl.GetInteger(GetPName.MaxElementIndex, out var maxElementIndex);
        Trace($"On this platform GL_MAX_ELEMENTS_VERTICES == {maxVertices}, GL_MAX_ELEMENTS_INDICES == {maxIndices}, GL_MAX_ELEMENT_INDEX = {maxElementIndex}");
    }

    public GL GetGL()
    {
        return _getGL();
    }

    public void ExecuteGraphicsThreadActions(float dt)
    {
        _graphicsThreadActions.RunPart(dt);
    }


    public void SetupDone()
    {
        _textureGenerator = I.Get<TextureGenerator>();
        _textureManager = I.Get<TextureManager>();
        _shaderManager = I.Get<ShaderManager>();
    }

    public SilkThreeD()
    {
        _engine = I.Get<Engine>();

        _listShaderUseCases.Add(new LightShaderUseCase());
    }
}