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
                    _bufferBakedFrame.Dispose();
                    _bufferBakedFrame = null;
                }

                Span<Matrix4x4> span = allBakedMatrices.AsSpan().Slice(offset, nBones);

                // Span<float> span = MemoryMarshal.Cast<Matrix4x4, float>(modelBakedFrame.BoneTransformations);
                _bufferBakedFrame = new BufferObject<Matrix4x4>(_gl, span, BufferTargetARB.UniformBuffer);
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
        }
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