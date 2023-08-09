using System;
using System.Collections.Generic;
using System.Data;
using System.Numerics;
using System.Text;
using engine;
using static engine.Logger;
using Silk.NET.OpenGL;
using Trace = System.Diagnostics.Trace;


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
    private readonly TextureGenerator _textureGenerator;
    private readonly TextureManager _textureManager;
    public TextureManager TextureManager
    {
        get => _textureManager;
    }
    private GL? _gl = null;

    private Matrix4x4 _matView;
    private Matrix4x4 _matProjection;

    private SkShaderEntry? _skInstanceShaderEntry;

    private int _nUploadedMeshes = 0;
    
    private class LightShaderPos
    {
        // Shader locations
        public int EnabledLoc;
        public int TypeLoc;
        public int PosLoc;
        public int TargetLoc;
        public int ColorLoc;
        public int Param1Loc;
    }

    /*
     * As the ambient lights are not per light but per total, we
     * have an extra location in the shader.
     */
    private int _ambientLoc;

    private LightShaderPos[]? _lightShaderPos = null;

    private readonly engine.WorkerQueue _graphicsThreadActions = new("Splash.silk.graphicsThreadActions");
    
    
    public int CheckError(string what)
    {
        int err = 0;
        while (true)
        {
            var error = _gl.GetError();
            if (error != GLEnum.NoError)
            {
                Error($"Found OpenGL {what} error {error}");
                err += (int)error;
            }
            else
            {
                // Console.WriteLine($"OK: {what}");
                return err;
            }
        }
    }
    
    
    // Create a light and get shader locations
    private void _compileLightLocked(
        in LightShaderPos lightShaderPos, int lightIndex, ref SkShader sh)
    {
        string enabledName = $"lights[{lightIndex}].enabled";
        string typeName = $"lights[{lightIndex}].type";
        string posName = $"lights[{lightIndex}].position";
        string targetName = $"lights[{lightIndex}].target";
        string colorName = $"lights[{lightIndex}].color";
        string param1Name = $"lights[{lightIndex}].param1";

        lightShaderPos.EnabledLoc = (int)sh.GetUniform(enabledName);
        lightShaderPos.TypeLoc = (int)sh.GetUniform(typeName);
        lightShaderPos.PosLoc = (int)sh.GetUniform(posName);
        lightShaderPos.TargetLoc = (int)sh.GetUniform(targetName);
        lightShaderPos.ColorLoc = (int)sh.GetUniform(colorName);
        lightShaderPos.Param1Loc = (int)sh.GetUniform(param1Name);

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
                _ambientLoc = (int)sh.GetUniform("col4Ambient");

            }

            return _lightShaderPos[index];
        }
    }


    /**
     * Update lights value in shader
     */
    private unsafe void _applyLightValues(ref SkShader sh, int index, in Light light)
    {
        try
        {
            var lightShaderPos = _getLightShaderPos(index, ref sh);
            bool checkLights = true;

            // Send to shader light enabled state and type
            sh.SetUniform(lightShaderPos.EnabledLoc, (light.enabled ? 1 : 0));
            if (checkLights) CheckError($"Set Uniform light enabled {index}");
            sh.SetUniform(lightShaderPos.TypeLoc, (int)light.type);
            if (checkLights) CheckError($"Set Uniform light type {index}");

            // Send to shader light position values
            Vector3 position = new(light.position.X, light.position.Y, light.position.Z);
            sh.SetUniform(lightShaderPos.PosLoc, position);
            if (checkLights) CheckError($"Set Uniform light position {index}");

            // Send to shader light target position values
            Vector3 target = new(light.target.X, light.target.Y, light.target.Z);
            sh.SetUniform(lightShaderPos.TargetLoc, target);
            if (checkLights) CheckError($"Set Uniform light target {index}");

            // Send to shader light color values
            Vector4 color = light.color;
            sh.SetUniform(lightShaderPos.ColorLoc, color);
            if (checkLights) CheckError($"Set Uniform light color {index}");

            float param1 = light.param1;
            sh.SetUniform(lightShaderPos.Param1Loc, param1);
        }
        catch (Exception e)
        {
            Error("Problem applying light values");
            // throw e;
        }
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
        SkShader? sh = ((SkShaderEntry)aShaderEntry).SkShader;
        if (null == sh)
        {
            return;
        }

        sh.Use();
        _applyAllLights(listLights, ref sh);
        CheckError( "applyAllLights");
    }
    
    public void ApplyAmbientLights(in Vector4 colAmbient, in AShaderEntry aShaderEntry)
    {
        SkShader? sh = ((SkShaderEntry)aShaderEntry).SkShader;
        bool checkLights = false;
        if (null == sh)
        {
            return;
        }
        sh.SetUniform(_ambientLoc,
            // Vector4.Zero
            colAmbient
        );
        if( checkLights ) CheckError($"Set Uniform ambient light");
    }


    private GL _getGL()
    {
        if (null == _gl)
        {
            ErrorThrow("_gl is null.", (m)=>new InvalidOperationException(m));
            throw new InvalidOperationException("goo");
        }

        return _gl;
    }
    
    private SkShaderEntry _createDefaultShader()
    {
        var gl = _getGL();
        SkShaderEntry skShaderEntry = new SkShaderEntry();
        skShaderEntry.SkShader = new SkShader(
            gl,
            shadercode.LightingVS.GetShaderCode(),
            shadercode.LightingFS.GetShaderCode()
        );
        return skShaderEntry;
    }

    
    public SkShaderEntry GetInstanceShaderEntry()
    {
        lock (_lo)
        {
            if (null == _skInstanceShaderEntry)
            {
                _skInstanceShaderEntry = _createDefaultShader();
            }
        }
        return _skInstanceShaderEntry;
    }

    public SkShader GetInstanceShader()
    {
        SkShaderEntry skShaderEntry = GetInstanceShaderEntry();
        SkShader? skShader = skShaderEntry.SkShader;
        if (null == skShader)
        {
            Error("instance shader is null");
            throw new InvalidOperationException("instance shader is null");
        }

        return skShader;
    }

    private void _loadMaterialToShader(in SkShader sh, in SkMaterialEntry skMaterialEntry)
    {
        try
        {
            //sh.SetUniform("texture0");
            SkTextureEntry? skDiffuseTextureEntry = skMaterialEntry.SkDiffuseTexture;
            if (skDiffuseTextureEntry != null && skDiffuseTextureEntry.IsUploaded())
            {
                SkTexture? skTexture = skDiffuseTextureEntry.SkTexture;
                if (skTexture != null)
                {
                    skTexture.ActiveAndBind(TextureUnit.Texture0);
                    CheckError("Bind Texture0");
                }
            }

            SkTextureEntry? skEmissiveTextureEntry = skMaterialEntry.SkEmissiveTexture;
            if (skEmissiveTextureEntry != null && skEmissiveTextureEntry.IsUploaded())
            {
                SkTexture? skTexture = skEmissiveTextureEntry.SkTexture;
                if (skTexture != null)
                {
                    skTexture.ActiveAndBind(TextureUnit.Texture2);
                    CheckError("Bind Texture 2");
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
        }
        catch (Exception e)
        {
            Error("Error loading material to shader");
        }
    }

    private void _unloadMaterialFromShader()
    {
        var gl = _getGL();

        gl.ActiveTexture(TextureUnit.Texture0);
        CheckError("unload ActiveTexture0");
        gl.BindTexture(TextureTarget.Texture2D, 0);
        CheckError("unbund texturetarget 0");
        gl.ActiveTexture(TextureUnit.Texture2);
        CheckError("unload ActiveTexture2");
        gl.BindTexture(TextureTarget.Texture2D, 0);
        CheckError("unbund texturetarget 2");
    }


    private void _printUniforms()
    {
        SkShader skShader = GetInstanceShader();
        uint shaderHandle = skShader.Handle;
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

    private static readonly  bool _useInstanceRendering = true;

    public void FinishUploadOnly(in AMeshEntry aMeshEntry)
    {
        // TXWTODO: Some of these calls have been required.
        // SkMeshEntry skMeshEntry = ((SkMeshEntry)aMeshEntry);
        // skMeshEntry.vao.BindVertexArray();
        // _gl.BindVertexArray(0);
        // _gl.BindBuffer(GLEnum.ArrayBuffer, 0);

    }
    
    public unsafe void DrawMeshInstanced(
        in AMeshEntry aMeshEntry,
        in AMaterialEntry aMaterialEntry,
        in Span<Matrix4x4> spanMatrices,
        in int nMatrices)
    {
        var gl = _getGL();
        
        CheckError("Beginning of DrawMeshInstanced");
        SkMeshEntry skMeshEntry = ((SkMeshEntry)aMeshEntry);
        //VertexArrayObject skMesh = skMeshEntry.vao;

        SkMaterialEntry skMaterialEntry = ((SkMaterialEntry)aMaterialEntry);

        /*
         * 1. set shader uniforms if the material has changed
         * 2. Actually draw mesh.
         */
        SkShader sh = GetInstanceShader();
        sh.Use();

        /*
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
            CheckError("Bind Vertex Array");
            bMatrices = new BufferObject<Matrix4x4>(_gl, spanMatrices, BufferTargetARB.ArrayBuffer);
            CheckError("New Buffer Object");
            bMatrices.BindBuffer();
            CheckError("Bind Buffer");
            uint locInstanceMatrices = sh.GetAttrib("instanceTransform");
            for (uint i = 0; i < 4; ++i)
            {
                gl.EnableVertexAttribArray(locInstanceMatrices + i);
                CheckError("Enable vertex array in instances");
                gl.VertexAttribPointer(
                    locInstanceMatrices + i,
                    4,
                    VertexAttribPointerType.Float,
                    false,
                    16 * (uint)sizeof(float),
                    (void*)(sizeof(float) * i * 4)
                );
                CheckError("Enable vertex attribute pointer n");
                gl.VertexAttribDivisor(locInstanceMatrices + i, 1);
                CheckError("attrib divisor");
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
            CheckError("instance vertex array bind");
        }
        
        /*
         * Setup view and projection matrix.
         * We need a combined view and projection matrix
         */


        // Matrix4x4 matTotal = mvp * Matrix4x4.Transpose(spanMatrices[0]);
        // Vector4 v0 = Vector4.Transform(new Vector4( skMeshEntry.JMesh.Vertices[0], 0f), matTotal);
        if (_useInstanceRendering) 
        {
            Matrix4x4 mvp = _matView * _matProjection;
            sh.SetUniform("mvp", mvp);
            if (skMeshEntry.JMesh.Vertices.Count > 65535)
            {
                Error($"Trying to render mesh {skMeshEntry.vao.Handle} with too much mesh vertices at once ({skMeshEntry.JMesh.Vertices.Count})");
            }
            if (skMeshEntry.JMesh.Indices.Count > 65535)
            {
                Error($"Trying to render mesh {skMeshEntry.vao.Handle} with too much mesh vertices at once ({skMeshEntry.JMesh.Indices.Count})");
            }
            if (nMatrices > 1023)
            {
                Error($"Trying to render mesh {skMeshEntry.vao.Handle} with too much mesh instances at once ({nMatrices})");
            }
            gl.DrawElementsInstanced(
                PrimitiveType.Triangles,
                (uint)skMeshEntry.JMesh.Indices.Count,
                GLEnum.UnsignedShort,
                (void*)0,
                (uint)nMatrices);
        }
        else
        {
            if (skMeshEntry.JMesh.Vertices.Count > 65535)
            {
                Error($"Trying to render mesh {skMeshEntry.vao.Handle} with too much mesh vertices at once ({skMeshEntry.JMesh.Vertices.Count})");
            }
            if (skMeshEntry.JMesh.Indices.Count > 65535)
            {
                Error($"Trying to render mesh {skMeshEntry.vao.Handle} with too much mesh vertices at once ({skMeshEntry.JMesh.Indices.Count})");
            }

            for (int i = 0; i < nMatrices; ++i)
            {
                Matrix4x4 mvpi = Matrix4x4.Transpose(spanMatrices[i]) * _matView * _matProjection;
                sh.SetUniform("mvp", mvpi);
                CheckError("upload mvpi");
                gl.DrawElements(
                    PrimitiveType.Triangles,
                    (uint)skMeshEntry.JMesh.Indices.Count,
                    DrawElementsType.UnsignedShort,
                    (void*)0);
                CheckError("draw elements");
            }
        }
        
        gl.BindVertexArray(0);
        gl.BindBuffer( GLEnum.ArrayBuffer, 0);
        gl.BindBuffer( GLEnum.ElementArrayBuffer, 0);
        _unloadMaterialFromShader();
        
        // TXWTODO: Shall we really always disable the current program?
        // gl.UseProgram(0);

        if (null != bMatrices)
        {
            bMatrices.Dispose();
        }

    }   

    public void UploadMesh(in AMeshEntry aMeshEntry)
    {
        SkMeshEntry skMeshEntry = ((SkMeshEntry)aMeshEntry);
        if (!skMeshEntry.IsUploaded())
        {
            skMeshEntry.Upload(_getGL());
            if (CheckError("AfterUpload mesh") < 0)
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
    public AMeshEntry CreateMeshEntry(in engine.joyce.Mesh jMesh)
    {
        MeshGenerator.CreateSilkMesh(jMesh, out var skMeshEntry);
        return skMeshEntry;
    }

    public void UnloadMeshEntry(in AMeshEntry aMeshEntry)
    {
        SkMeshEntry skMeshEntry = (SkMeshEntry)aMeshEntry;
        _graphicsThreadActions.Enqueue(() =>
        {
            int nUploadedMeshes;
            if (skMeshEntry.IsUploaded())
            {
                skMeshEntry.Release(_getGL());
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

            throw new InvalidOperationException("not yet implemented");
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

        if (jMaterial.Texture != null && jMaterial.Texture.IsValid())
        {
            ATextureEntry? aTextureEntry = _textureManager.FindATexture(jMaterial.Texture);
            skMaterialEntry.SkDiffuseTexture = ((SkTextureEntry)aTextureEntry);
        }
        if (jMaterial.EmissiveTexture != null && jMaterial.EmissiveTexture.IsValid())
        {
            ATextureEntry? aEmissiveTextureEntry = _textureManager.FindATexture(jMaterial.EmissiveTexture);
            skMaterialEntry.SkEmissiveTexture = ((SkTextureEntry)aEmissiveTextureEntry);
        }
#if false
        {
            if (jMaterial.Texture == null)
            {
                Trace( "no texture found at all.");
            }
            ATextureEntry? aEmissiveTextureEntry = _textureManager.FindATexture(new engine.joyce.Texture("joyce://col00000000"));
            skMaterialEntry.SkEmissiveTexture = ((SkTextureEntry)aEmissiveTextureEntry);
        }
#endif
        
        skMaterialEntry.SetUploaded();

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
        // TXWTODO: Write me. Deprecate this by VIewMatrix and Projection Matrix
        // TXWTODO: Bad hack, remove me.
        {
            SkShaderEntry shader = GetInstanceShaderEntry();
            shader.SkShader.SetUniform("v3AbsPosView", vCamera);     
        }

    }

    public ARenderbuffer CreateRenderbuffer(in engine.joyce.Renderbuffer jRenderbuffer)
    {
        SkRenderbuffer skRenderbuffer = new SkRenderbuffer(jRenderbuffer);
        return skRenderbuffer;
    }
    
    public void UploadRenderbuffer(in ARenderbuffer aRenderbuffer)
    {
        SkRenderbuffer skRenderbuffer = ((SkRenderbuffer)aRenderbuffer);
        if (!skRenderbuffer.IsUploaded())
        {
            skRenderbuffer.Upload(_getGL(), _textureManager);
        }

    }

    
    public void UnloadRenderbuffer(in ARenderbuffer aRenderbuffer)
    {
        SkRenderbuffer skRenderbuffer = (SkRenderbuffer)aRenderbuffer;
        _graphicsThreadActions.Enqueue(() =>
        {
            if (skRenderbuffer.IsUploaded())
            {
                skRenderbuffer.Release(_getGL());
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
    }

    public GL GetGL()
    {
        return _getGL();
    }

    public void ExecuteGraphicsThreadActions(float dt)
    {
        _graphicsThreadActions.RunPart(dt);
    }
    

    public SilkThreeD(in engine.Engine engine)
    {
        _engine = engine;
        _textureGenerator = new(engine, this);
        //_createDefaultShader();
        // GetDefaultMaterial();
        _textureManager = new TextureManager(this);
    }
   
}