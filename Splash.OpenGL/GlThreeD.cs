using System;
using System.Collections.Generic;
using System.Data;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using BepuPhysics.Constraints;
using engine;
using engine.joyce;
using static engine.Logger;
using Splash.API.OpenGL;

namespace Splash.OpenGL;


public class GlThreeD : IThreeD
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
    private Matrix4x4 _m4View;
    private Matrix4x4 _m4Projection;
    private Vector3 _vCamera;
    private float _fogDistance;
    private Vector4 _v4FogColor = new(0.2f, 0.18f, 0.2f, 0.8f); 

    private int _nUploadedMeshes = 0;
    
    private LightCollector _currentLightCollector = null;
    
    private readonly engine.scheduler.WorkerQueue _graphicsThreadActions = new("Splash.silk.graphicsThreadActions");

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
    private GlRenderState _silkRenderState;


    GLAnimBuffers AnimStrategy = GLAnimBuffers.AnimUniform;

    /**
     * True when the context is desktop OpenGL 4.3 or newer.
     *
     * GL_DEBUG_OUTPUT, GL_DEBUG_OUTPUT_SYNCHRONOUS and GL_MAX_ELEMENT_INDEX are all
     * OpenGL 4.3 features. macOS caps out at 4.1, so issuing any of them there raises
     * GL_INVALID_ENUM. That is not harmless: the GL error flag is sticky, so the error
     * sat in the queue from startup until the first DrawMeshInstanced drained it and
     * reported it as "pre-existing" - on every single run. Real errors were then
     * indistinguishable from that startup noise.
     *
     * Set conservatively to false for OpenGL ES. GL_MAX_ELEMENT_INDEX does exist in
     * ES 3.0, but it is only read for a trace here, so there is nothing to gain by
     * probing it and a silent GL_INVALID_ENUM to lose on older ES contexts.
     */
    private readonly bool _hasGL43;

    /**
     * Kept from the constructor because KHR_debug capability cannot be decided there: on
     * ES below 3.2 it depends on the extension string, and there is no GL context yet.
     * SetGL asks GlDiagnostics.Detect once the context exists.
     */
    private readonly string _api = "";
    private readonly int _versionNumber;

    /**
     * Only the SSBO strategy carries a per-instance frame number (the instanceFrameno
     * vertex attribute). The UBO and uniform strategies upload a single bone pose per
     * draw call, so their batches must not mix animations or frames.
     */
    public bool HasPerInstanceAnimationFrames => AnimStrategy == GLAnimBuffers.AnimSSBO;
    
    
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
            if (_lastMaterialEntry == skMaterialEntry)
            {
                return;
            }

            if (_lastMaterialEntry != skMaterialEntry)
            {
                _unloadMaterialFromShader();
                _lastMaterialEntry = null;
            }

            /*
             * Setup new shader if required at all.
             */
            _silkRenderState.UseProgramEntry(sh, _setupProgramGlobals);

            if (_lastMaterialEntry == skMaterialEntry)
            {
                return;
            }

            _lastMaterialEntry = skMaterialEntry;
        }

        try
        {
            _silkRenderState.Texture0.UseTextureEntry(skMaterialEntry.SkDiffuseTexture);
            GlDbg.Check(_getGL());
            _silkRenderState.Texture2.UseTextureEntry(skMaterialEntry.SkEmissiveTexture);
            GlDbg.Check(_getGL());

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
            GlDbg.Check(_getGL());

            // sh.SetUniform("ambient", new Vector4(.2f, .2f, .2f, 0.0f));
            sh.SetUniform("texture0", 0);
            sh.SetUniform("texture2", 2);

            Material.ShaderFlags materialFlags = 0;
            if (jMaterial.AddInterior)
            {
                materialFlags |= Material.ShaderFlags.RenderInterior;
            }
            sh.SetUniform("materialFlags", (int) materialFlags);
            GlDbg.Check(_getGL());
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
#if false
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
#endif
        }
        catch (Exception e)
        {
            Error($"Error loading material to shader: {e}");
        }
    }


    private GlFrame _silkFrame = null;
    
    public void BeginRenderFrame(RenderFrame renderFrame)
    {
        /*
         * Reset peephole caches — external code (e.g. GlStateSaver in the
         * Avalonia host) may have changed GL state between frames.
         */
        _lastMaterialEntry = null;
        _silkRenderState.ResetCachedState();

        _silkFrame = new(_gl, renderFrame);
    }
    
    
    /*
     * Detach any pending programs from the pipeline.
     */
    public void EndRenderFrame()
    {
        if (null != _lastMaterialEntry)
        {
            _unloadMaterialFromShader();
        }

        if (null != _silkFrame)
        {
            _silkFrame.Dispose();
            _silkFrame = null;
        }
 
        _frameno++;
    }


    public void EndRenderPart()
    {
    }
    
    
    public void BeginRenderPart(RenderPart renderPart)
    {
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
    
    private List<IShaderUseCase> _listShaderUseCases = new();

    
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
        var gl = _getGL();

        shader.Use();
        GlDbg.Check(gl, $"program={shader.Handle}");

        /*
         * Before using the shader at all, make sure all our use cases are
         * resolved
         */
        _resolveProgramUseCases(shader);
        GlDbg.Check(gl);

        /*
         * Now specific calls.
         * FIXME: This needs a more beautiful API.
         */
        {
            LightShaderUseCaseLocs uc =
                shader.ShaderUseCases[LightShaderUseCase.StaticName]
                    as LightShaderUseCaseLocs;
            uc.Apply(gl, shader, _silkFrame.RenderFrame.LightCollector);
        }
        GlDbg.Check(gl);

        shader.SetUniform("fogDistance", _fogDistance);
        shader.SetUniform("col4Fog", _v4FogColor);
        shader.SetUniform("v3AbsPosView", _vCamera);
        shader.SetUniform("frameNo", _frameno);
        GlDbg.Check(gl);

        /*
         * Also load the locations for some programs from the shader.
         */
        _locInstanceMatrices = shader.GetAttrib("instanceTransform");

        //Trace($"Anim Strategy is {AnimStrategy}");
        switch (AnimStrategy)
        {
            case GLAnimBuffers.AnimSSBO:
                _locFrameno = shader.GetAttrib("instanceFrameno");
                _locNBones = shader.GetUniform("nBones");
                break;
            case GLAnimBuffers.AnimUBO:
                _locNBones = shader.GetUniform("nBones");
                break;
            case GLAnimBuffers.AnimUniform:
                _locBoneMatrices = shader.GetUniform("m4BoneMatrices");
                break;
            default:
                break;
        }

        _locMvp = shader.GetUniform("mvp");
        _locVertexFlags = shader.GetUniform("iVertexFlags");
        GlDbg.Check(gl);
    }

    
    public unsafe void DrawMeshInstanced(
        in AMeshEntry aMeshEntry,
        in AMaterialEntry aMaterialEntry,
        in AAnimationsEntry? aAnimationsEntry,
        in Span<Matrix4x4> spanMatrices,
        in Span<uint> spanFramenos,
        in int nMatrices,
        ModelAnimation? modelAnimation,
        uint frameno)
    {
        var gl = _getGL();

        GlDbg.Check(gl);

        if (false && _frameno % 300 == 0)
        {
            SkMeshEntry dbgMesh = ((SkMeshEntry)aMeshEntry);
            SkMaterialEntry dbgMat = ((SkMaterialEntry)aMaterialEntry);
            var dbgJMesh = dbgMesh.Params.JMesh;
            System.Console.Error.WriteLine(
                $"[DrawMeshInstanced] vao={dbgMesh.vao?.Handle}, uploaded={dbgMesh.IsUploaded()}, " +
                $"verts={dbgJMesh.Vertices.Count}, indices={dbgJMesh.Indices.Count}, instances={nMatrices}, " +
                $"program={dbgMat.SkProgram?.Handle ?? 0}, matUploaded={dbgMat.IsUploaded()}, " +
                $"fragShader={dbgMat.JMaterial.FragmentShader ?? "(default)"}, " +
                $"vertShader={dbgMat.JMaterial.VertexShader ?? "(default)"}");
            // Check GL state
            gl.GetInteger(GLEnum.CurrentProgram, out int curProg);
            gl.GetInteger(GLEnum.FramebufferBinding, out int curFbo);
            Span<int> vp = stackalloc int[4];
            gl.GetInteger(GLEnum.Viewport, vp);
            System.Console.Error.WriteLine(
                $"[DrawMeshInstanced] GL: program={curProg}, fbo={curFbo}, viewport=[{vp[0]},{vp[1]},{vp[2]},{vp[3]}]");
            GlDbg.Check(gl);
        }
        SkMeshEntry skMeshEntry = ((SkMeshEntry)aMeshEntry);
        //VertexArrayObject skMesh = skMeshEntry.vao;

        SkMaterialEntry skMaterialEntry = ((SkMaterialEntry)aMaterialEntry);
        SkAnimationsEntry? skAnimationsEntry = null;
        if (aAnimationsEntry != null && aAnimationsEntry != NullAnimationsEntry.Instance())
        {
            skAnimationsEntry = ((SkAnimationsEntry)aAnimationsEntry);
        }
        
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
        // Drain any pre-existing GL errors before _loadMaterialToShader
        GlDbg.Check(gl);
        _loadMaterialToShader(sh, skMaterialEntry);
        GlDbg.Check(gl);

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
        BufferObject<uint>? bFramenos = null;

        if (_useInstanceRendering)
        {
            /*
             * Bind the mesh itself.
             */
            skMeshEntry.vao.BindVertexArray();
            GlDbg.Check(gl, $"vao={skMeshEntry.vao.Handle}");
            GlDbg.Check(gl);
            
            _silkFrame.RegisterInstanceBuffer(spanMatrices);

            int vertexFlags = 4;
            if (skAnimationsEntry != null)
            {
                /*
                 * Adjust the rendering method according to the platform
                 */
                switch (skAnimationsEntry.AnimStrategy)
                {
                    case GLAnimBuffers.AnimSSBO:
                    {
                        /*
                         * SSBO: We just use the ssbo previously uploaded.
                         */
                        vertexFlags = 3;
                        _silkRenderState.UseBoneMatricesSSBO(skAnimationsEntry.SSBOAnimations);
                    }
                        break;
                    case GLAnimBuffers.AnimUniform:
                    {
                        var model = skAnimationsEntry.Model;
                        if (model != null && modelAnimation != null)
                        {
                            /*
                             * We upload the per frame data.
                             */
                            vertexFlags = 2;

                            /*
                             * If we are supposed to load bone animations, let's do that.
                             */
                            int nBones = model.Skeleton!.NBones;
#if false
                            Span<float> span =
                                MemoryMarshal.Cast<Matrix4x4, float>(modelBakedFrame.BoneTransformations);
#else
                            var allBakedMatrices = model.AnimationCollection.AllBakedMatrices;

                            /*
                             * Skip the upload rather than read a foreign clip (or run off the
                             * end of the array) if the frame is not addressable for this
                             * animation - see ModelAnimation.TryGetBakedFrameOffset.
                             */
                            /*
                             * frameno is already the global baked frame (see MeshBatch.Add),
                             * so it must not be offset by FirstFrame a second time here.
                             */
                            if (null == allBakedMatrices
                                || !ModelAnimation.TryGetBakedFrameOffset(
                                    frameno, nBones, allBakedMatrices.Length, out int frameOffset))
                            {
                                /*
                                 * Fall back to unanimated rendering, otherwise the shader
                                 * would skin against whatever is left in the uniform.
                                 */
                                vertexFlags = 4;
                                break;
                            }

                            // .AsSpan() is required from C# 14 on: see SkAnimationsEntry.Upload().
                            Span<float> span =
                                MemoryMarshal.Cast<Matrix4x4, float>(allBakedMatrices.AsSpan())
                                    .Slice(16 * frameOffset, 16 * nBones);
#endif
                            _gl.UniformMatrix4((int)_locBoneMatrices,
                                (uint)nBones,
                                false, span);
                        }
                    }
                        break;
                    case GLAnimBuffers.AnimUBO:
                    {

                        var model = skAnimationsEntry.Model;
                        if (model != null && modelAnimation != null)
                        {
                            vertexFlags = 1;
                            int nBones = model.Skeleton!.NBones;
                            
                            sh.SetUniform(_locNBones, (uint)nBones);
                            _silkRenderState.UseBoneMatricesFrameUBO(model, modelAnimation, frameno);
                        }
                    }
                        break;
                    default:
                        vertexFlags = 4;
                        break;
                }
                
            }
            sh.SetUniform(_locVertexFlags, vertexFlags);

            /*
             * Upload the matrix array for instanced rendering.
             */
            bMatrices = new BufferObject<Matrix4x4>(_gl, spanMatrices, BufferTargetARB.ArrayBuffer);
            GlDbg.Check(gl);

            if (false && _frameno % 300 == 0)
            {
                System.Console.Error.WriteLine(
                    $"[DrawMeshInstanced] _locInstanceMatrices={_locInstanceMatrices}, _locMvp={_locMvp}, _locVertexFlags={_locVertexFlags}");
            }   

            for (uint i = 0; i < 4; ++i)
            {
                gl.EnableVertexAttribArray((uint) _locInstanceMatrices + i);
                GlDbg.Check(gl, $"loc={(uint)_locInstanceMatrices + i}");
                gl.VertexAttribPointer(
                    (uint) _locInstanceMatrices + i,
                    4,
                    VertexAttribPointerType.Float,
                    false,
                    16 * (uint)sizeof(float),
                    (void*)(sizeof(float) * i * 4)
                );
                GlDbg.Check(gl, $"loc={(uint)_locInstanceMatrices + i}");
                gl.VertexAttribDivisor((uint) _locInstanceMatrices + i, 1);
                GlDbg.Check(gl, $"loc={(uint)_locInstanceMatrices + i}");
            }

            if (AnimStrategy == GLAnimBuffers.AnimSSBO)
            {
                if (spanFramenos != null)
                {
                    int nBones = aAnimationsEntry?.Model?.Skeleton?.NBones ?? 1;
                    if (nBones > 1)
                    {
                        if (skAnimationsEntry == null)
                        {
                            int a = 1;
                        }
                    }

                    sh.SetUniform(_locNBones, (uint)nBones);
                    GlDbg.Check(gl);

                    /*
                     * Upload the frame number array for instanced rendering
                     */
                    bFramenos = new BufferObject<uint>(_gl, spanFramenos, BufferTargetARB.ArrayBuffer);
                    GlDbg.Check(gl);
                    gl.EnableVertexAttribArray((uint)_locFrameno);
                    GlDbg.Check(gl);
                    gl.VertexAttribIPointer((uint)_locFrameno, 1,
                        VertexAttribIType.UnsignedInt, 0, (void*)0);
                    GlDbg.Check(gl);
                    gl.VertexAttribDivisor((uint)_locFrameno, 1);
                    GlDbg.Check(gl);
                }
            } 
        }
        else
        {
            skMeshEntry.vao.BindVertexArray();
            GlDbg.Check(gl);
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
            Matrix4x4 m4Mvp = _m4View * _m4Projection;
            sh.SetUniform(_locMvp, m4Mvp);
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
            // Drain any accumulated GL errors before the draw call
            GlDbg.Check(gl, $"vao={skMeshEntry.vao?.Handle}");
            gl.DrawElementsInstanced(
                PrimitiveType.Triangles,
                (uint)jMesh.Indices.Count,
                GLEnum.UnsignedShort,
                (void*)0,
                (uint)nMatrices);
            GlDbg.Check(gl, $"indices={jMesh.Indices.Count}, instances={nMatrices}, vao={skMeshEntry.vao?.Handle}");
            GlDbg.Check(gl);
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
                Matrix4x4 mvpi = Matrix4x4.Transpose(spanMatrices[i]) * _m4View * _m4Projection;
                sh.SetUniform(_locMvp, mvpi);
                GlDbg.Check(gl);
                gl.DrawElements(
                    PrimitiveType.Triangles,
                    (uint)jMesh.Indices.Count,
                    DrawElementsType.UnsignedShort,
                    (void*)0);
                GlDbg.Check(gl);
            }
        }
        
        gl.BindVertexArray(0);
        gl.BindBuffer( GLEnum.ArrayBuffer, 0);
        gl.BindBuffer( GLEnum.ElementArrayBuffer, 0);
        
        if (null != bMatrices)
        {
            _silkFrame.ListFrameDisposables.Add(bMatrices);
        }

        if (null != bFramenos)
        {
            _silkFrame.ListFrameDisposables.Add(bFramenos);
        }

    }   
    

    public void UploadMeshEntry(in AMeshEntry aMeshEntry)
    {
        var gl = _getGL();
        SkMeshEntry skMeshEntry = ((SkMeshEntry)aMeshEntry);
        if (!skMeshEntry.IsUploaded())
        {
            skMeshEntry.Upload();
            GlDbg.Check(gl);
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

    public AAnimationsEntry CreateAnimationsEntry(in engine.joyce.Model? jModel)
    {
        if (null == jModel)
        {
            return NullAnimationsEntry.Instance();
        }

        var skAnimationsEntry = new SkAnimationsEntry(_getGL(), jModel, AnimStrategy);
        return skAnimationsEntry;
    }
    

    public void UploadAnimationsEntry(in AAnimationsEntry aAnimationsEntry)
    {
        if (!aAnimationsEntry.IsUploaded())
        {
            aAnimationsEntry.Upload();
        }
    }

    public void UnloadAnimationsEntry(in AAnimationsEntry aAnimationsEntry)
    {
        SkAnimationsEntry skAnimationsEntry = (SkAnimationsEntry)aAnimationsEntry;
        _graphicsThreadActions.Enqueue(() =>
        {
            if (skAnimationsEntry.IsUploaded())
            {
                skAnimationsEntry.Release();
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
                // Warning($"Unable to upload texture {jMaterial.Texture.Key}");
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
                // Warning($"Unable to upload texture {jMaterial.EmissiveTexture.Key}");
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


    private int _locInstanceMatrices = 0;
    private int _locVertexFlags = 0;
    private int _locMvp = 0;
    private int _locFrameno = 0;
    private int _locNBones = 0;
    private int _locBoneMatrices = 0;

    
    public void SetCameraPos(in Vector3 vCamera)
    {
        _vCamera = vCamera;
    }


    public void SetFogDistance(float fogDistance)
    {
        _fogDistance = fogDistance;
    }


    public void SetFogColor(Vector4 fogColor)
    {
        _v4FogColor = fogColor;
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
        _m4View = matView;
    }


    /**
     * Set the current projection matrix
     * @param matProjection
     *    perspective projection matrix, .NET order.
     */
    public void SetProjectionMatrix(in Matrix4x4 matProjection)
    {
        _m4Projection = matProjection;
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
        /*
         * KHR_debug. The callback is what makes this useful - without it the driver only
         * populated a debug message log nobody read, which was the state here until
         * GlDiagnostics was added.
         *
         * Gated on the DEBUG capability, not on _hasGL43. Those are different questions and
         * conflating them is what kept mobile polling: _hasGL43 also selects the SSBO
         * animation strategy and is false for ES by construction, while ES 3.2 has KHR_debug
         * in core. GL_DEBUG_OUTPUT and GL_DEBUG_OUTPUT_SYNCHRONOUS carry the same token
         * values under the KHR extension, so the two Enable/Disable calls need no variant.
         *
         * SYNCHRONOUS stays OFF. It makes the callback fire on the offending call's stack,
         * which is exactly what you want when hunting a specific fault, but it serialises
         * the driver - punishing on desktop, worse on a tile-based mobile GPU.
         */
        var debugApi = GlDiagnostics.Detect(_gl, _api, _versionNumber);
        if (debugApi != GlDiagnostics.DebugApi.None)
        {
            _gl.Enable(EnableCap.DebugOutput);
            _gl.Disable(EnableCap.DebugOutputSynchronous);
        }
        GlDbg.Init(_gl, debugApi);
        _gl.Enable(EnableCap.DepthClamp);
        _gl.Enable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.ScissorTest);
        _gl.Disable(EnableCap.StencilTest);

        _gl.GetInteger(GetPName.MaxElementsVertices, out var maxVertices);
        _gl.GetInteger(GetPName.MaxElementsIndices, out var maxIndices);
        if (_hasGL43)
        {
            _gl.GetInteger(GetPName.MaxElementIndex, out var maxElementIndex);
            Trace($"On this platform GL_MAX_ELEMENTS_VERTICES == {maxVertices}, GL_MAX_ELEMENTS_INDICES == {maxIndices}, GL_MAX_ELEMENT_INDEX = {maxElementIndex}");
        }
        else
        {
            Trace($"On this platform GL_MAX_ELEMENTS_VERTICES == {maxVertices}, GL_MAX_ELEMENTS_INDICES == {maxIndices}, GL_MAX_ELEMENT_INDEX = n/a (needs GL 4.3)");
        }

        /*
         * Report anything the setup above raised, here, rather than leaving it in the
         * queue for the first DrawMeshInstanced to find and blame on "pre-existing"
         * state. An error surfacing here means one of the calls above is unsupported on
         * this context.
         */
        GlDbg.Check(_gl, $"{engine.GlobalSettings.Get("platform.threeD.API")} {engine.GlobalSettings.Get("platform.threeD.API.version")}");

        _silkRenderState = new(_gl);
    }

    /**
     * Internal on purpose (WP-0.2): returning a Silk.NET GL keeps a graphics-API type out of
     * anything Splash/ or an embedding host can reach. The only caller is
     * Platform.RenderExternalFrame, in this same project.
     */
    internal GL GetGL()
    {
        return _getGL();
    }

    public void Execute(Action action)
    {
        _graphicsThreadActions.Enqueue(action);
    }
    
    public void ExecuteGraphicsThreadActions(float dt)
    {
        _graphicsThreadActions.RunPart(dt);
    }


    public void SetupDone()
    {
        _textureGenerator = I.Get<TextureGenerator>();
        _textureManager = I.Get<TextureManager>();
        _shaderManager = (I.Get<ModuleFactory>().FindModule(typeof(ShaderManager), true) as ShaderManager)!;
    }

    public GlThreeD()
    {
        _engine = I.Get<Engine>();
        _listShaderUseCases.Add(new LightShaderUseCase());
        string api = engine.GlobalSettings.Get("platform.threeD.API");
        string version = engine.GlobalSettings.Get("platform.threeD.API.version");
        _api = api;

        /*
         * Parse numerically. The previous String.Compare(version, "430") happens to be
         * right for equal-length values like "410", but is an ordinal string compare:
         * it would rank any shorter or longer version string by character order rather
         * than by magnitude.
         */
        if (!int.TryParse(version, out var versionNumber))
        {
            Warning($"Unparseable \"platform.threeD.API.version\": \"{version}\". "
                    + "Assuming the lowest capability level.");
            versionNumber = 0;
        }
        _versionNumber = versionNumber;

        if (api == "OpenGL")
        {
            _hasGL43 = versionNumber >= 430;
            AnimStrategy = _hasGL43 ? GLAnimBuffers.AnimSSBO : GLAnimBuffers.AnimUBO;
        }
        else if (api == "OpenGLES")
        {
            _hasGL43 = false;
            AnimStrategy = GLAnimBuffers.AnimUBO;
        }
        else
        {
            ErrorThrow($"Invalid graphics API setup in global config \"platform.threeD.API\": \"{api}\".",
                m => new InvalidOperationException(m));
            return;
        }
    }
}