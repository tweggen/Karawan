using System;
using System.Numerics;
using System.Runtime.InteropServices;
using engine.joyce;
using Silk.NET.OpenGL;

namespace Splash.Silk;

public class SilkRenderState
{
    private GL _gl;

    private SkProgramEntry _lastProgramEntry = null;

    public SilkTextureChannelState Texture0;
    public SilkTextureChannelState Texture2;

    public BufferObject<float>? BoneMatrices;

    private bool _isBoundModelBakedFrame = false;
    private ModelBakedFrame _modelBakedFrame;
    private BufferObject<Matrix4x4>? _bufferBakedFrame;

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


    public void UseBoneMatricesFrameUBO(engine.joyce.ModelBakedFrame modelBakedFrame)
    {
        /*
         * Create appropriate buffer object if not done yet.
         */
        if (_modelBakedFrame != modelBakedFrame)
        {
            if (_bufferBakedFrame != null)
            {
                // TXWTODO: Add to frame disposals.
                _bufferBakedFrame.Dispose();
                _bufferBakedFrame = null;
                _isBoundModelBakedFrame = false;
            }
            
            if (modelBakedFrame != null)
            {
                // Span<float> span = MemoryMarshal.Cast<Matrix4x4, float>(modelBakedFrame.BoneTransformations);
                _bufferBakedFrame = new BufferObject<Matrix4x4>(_gl, modelBakedFrame.BoneTransformations, BufferTargetARB.UniformBuffer);
                _modelBakedFrame = modelBakedFrame;
                _isBoundModelBakedFrame = false;
            }
        }
        
        /*
         * Bind buffer object if not done yet.
         */
        if (!_isBoundModelBakedFrame)
        {
            _bufferBakedFrame.BindBufferBase(0);
        }
    }
    
    
    public void UseBoneMatricesSSBO(BufferObject<float>? boneMatrices)
    {
        if (BoneMatrices == boneMatrices) return;

        BoneMatrices = boneMatrices;
        
        boneMatrices.BindBufferBase(0);
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

    
    public SilkRenderState(GL gl)
    {
        _gl = gl;
        Texture0 = new(gl, TextureUnit.Texture0);
        Texture2 = new(gl, TextureUnit.Texture2);
    }
}