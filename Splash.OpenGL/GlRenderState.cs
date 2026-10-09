using System;
using System.Numerics;
using System.Runtime.InteropServices;
using engine.joyce;
using Splash.API.OpenGL;

namespace Splash.OpenGL;

public class GlRenderState
{
    private GL _gl;

    private SkProgramEntry _lastProgramEntry = null;

    public GlTextureChannelState Texture0;
    public GlTextureChannelState Texture2;

    public BufferObject<float>? BoneMatrices;

    private bool _isBoundModelBakedFrame = false;
    private ModelAnimation _modelAnimation = null;
    private uint _frameno = 0;
    private BufferObject<Matrix4x4>? _bufferBakedFrame;

    private int _uboAnimIndex = -1;

    /*
     * What is bound at uniform-buffer binding 0, as far as we know. Null after a frame
     * boundary or after the bound buffer was deleted (deleting a bound buffer reverts the
     * binding to zero).
     */
    private BufferObject<Matrix4x4>? _boundBoneMatricesUBO;

    /*
     * A zero-filled buffer the size of the shader's BoneMatrices block, bound whenever no
     * animation frame is. See EnsureBoneMatricesUBOBound.
     */
    private BufferObject<Matrix4x4>? _uboPlaceholder;

    /*
     * Staging for one frame's bones, padded to MaxBones. The UBO must be at least as large
     * as the block it backs (GL_UNIFORM_BLOCK_DATA_SIZE, 120 mat4 = 7680 bytes); a buffer
     * holding only the model's nBones matrices is undefined behaviour on GLES.
     */
    private readonly Matrix4x4[] _frameStaging = new Matrix4x4[engine.joyce.Constants.MaxBones];
    
    private int _silkAnimMethod = -1;
    
    private void _unloadProgramEntry()
    {
        if (null == _lastProgramEntry)
        {
            return;
        }

        var pe = _lastProgramEntry;
        _lastProgramEntry = null;
        _silkAnimMethod = -1;
        _isBoundModelBakedFrame = false;
        
        // TXWTODO: Why is that? That is wrong.
        _gl.UseProgram(pe.Handle);
    }


    public void UseBoneMatricesFrameUBO(Model model, ModelAnimation? modelAnimation, uint frameno)
    {
        int nBones = model.Skeleton!.NBones;

        /*
         * Create appropriate buffer object if not done yet.
         */
        if (_modelAnimation != modelAnimation || _frameno != frameno)
        {
            var allBakedMatrices = model.AnimationCollection.AllBakedMatrices;

            /*
             * Resolve the offset before touching the currently bound buffer: if the
             * frame cannot be addressed we keep rendering the previous pose rather
             * than reading a foreign clip or throwing out of the render loop.
             *
             * frameno is already the global baked frame (see MeshBatch.Add), so it
             * must not be offset by FirstFrame a second time here.
             */
            if (modelAnimation != null
                && allBakedMatrices != null
                && ModelAnimation.TryGetBakedFrameOffset(
                    frameno, nBones, allBakedMatrices.Length, out int offset))
            {
                if (_bufferBakedFrame != null)
                {
                    // TXWTODO: Add to frame disposals.
                    if (_boundBoneMatricesUBO == _bufferBakedFrame)
                    {
                        _boundBoneMatricesUBO = null;
                    }
                    _bufferBakedFrame.Dispose();
                    _bufferBakedFrame = null;
                }

                int nCopy = Math.Min(nBones, _frameStaging.Length);
                allBakedMatrices.AsSpan().Slice(offset, nCopy).CopyTo(_frameStaging);

                _bufferBakedFrame = new BufferObject<Matrix4x4>(_gl, _frameStaging, BufferTargetARB.UniformBuffer);
                _modelAnimation = modelAnimation;
                _frameno = frameno;
                _isBoundModelBakedFrame = false;
            }
        }

        /*
         * Bind buffer object if not done yet.
         */
        if (!_isBoundModelBakedFrame && _bufferBakedFrame != null)
        {
            if (-1 == _uboAnimIndex)
            {
                _uboAnimIndex = _lastProgramEntry.GetUniformBlock("m4BoneMatrices");
            }

            _bufferBakedFrame.BindBufferBase(0);
            _boundBoneMatricesUBO = _bufferBakedFrame;
        }
    }


    /**
     * Make sure uniform-buffer binding 0 holds a valid, full-size buffer.
     *
     * Under the UBO animation strategy (GLES, desktop GL below 4.3) the vertex shader's
     * BoneMatrices block is ACTIVE in every program - skinning is selected at runtime by
     * iVertexFlags, so the compiler cannot drop it - and GLES requires a buffer behind
     * every active block for every draw, used or not. Only skinned draws ever bound one,
     * so a screen with no animated character (the start menu) drew with nothing bound:
     * "Program does not have a valid uniform buffer object ... for active uniform block",
     * once per draw call on Android. Desktop NVIDIA tolerates it silently.
     *
     * Non-skinned draws do not read the block, so whatever is already bound - the last
     * animation frame - is fine; only an empty binding is not.
     */
    public void EnsureBoneMatricesUBOBound()
    {
        if (_boundBoneMatricesUBO != null)
        {
            return;
        }

        var buffer = _bufferBakedFrame;
        if (null == buffer)
        {
            if (null == _uboPlaceholder)
            {
                _uboPlaceholder = new BufferObject<Matrix4x4>(
                    _gl, new Matrix4x4[engine.joyce.Constants.MaxBones], BufferTargetARB.UniformBuffer);
            }

            buffer = _uboPlaceholder;
        }

        buffer.BindBufferBase(0);
        _boundBoneMatricesUBO = buffer;
    }
    
    
    public void UseBoneMatricesSSBO(BufferObject<float>? boneMatrices)
    {
        if (BoneMatrices == boneMatrices) return;

        BoneMatrices = boneMatrices;
        
        boneMatrices.BindBufferBase(0);
    }
    
    
    /// <summary>
    /// Reset all cached state without issuing GL calls.
    /// Must be called at frame boundaries when external code (e.g. GlStateSaver)
    /// may have changed the actual GL state behind our back.
    /// </summary>
    public void ResetCachedState()
    {
        _lastProgramEntry = null;
        _isBoundModelBakedFrame = false;
        _boundBoneMatricesUBO = null;
        _modelAnimation = null;
        BoneMatrices = null;
        Texture0.ResetCachedState();
        Texture2.ResetCachedState();
    }


    public void UseProgramEntry(SkProgramEntry sh, Action<SkProgramEntry> firstTimeFunc)
    {
        if (_lastProgramEntry == sh) return;

        _lastProgramEntry = sh;
        firstTimeFunc(sh);
    }


    public void UnloadProgramEntry(SkProgramEntry sh)
    {
        _unloadProgramEntry();
    }

    
    public GlRenderState(GL gl)
    {
        _gl = gl;
        Texture0 = new(gl, TextureUnit.Texture0);
        Texture2 = new(gl, TextureUnit.Texture2);
    }
}