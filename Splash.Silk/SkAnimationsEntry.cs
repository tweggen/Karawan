using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.OpenGL;
using static engine.Logger;

namespace Splash.Silk;

public class SkAnimationsEntry : AAnimationsEntry
{
    public GL _gl;

    public BufferObject<float>? SSBOAnimations;
    private bool _isUploaded = false;

    private static int _nAnimations = 0;

    private bool _traceAnimations = true;

    public Flags.GLAnimBuffers AnimStrategy;


    /**
     * Upload the mesh to the GPU.
     */
    public override void Upload()
    {
        /*
         * If we are using anim buffer object, create and upload it here once.
         */
        switch (AnimStrategy)
        {
            case Flags.GLAnimBuffers.AnimSSBO:
                if (Model != null)
                {
                    Span<float> span = MemoryMarshal.Cast<Matrix4x4, float>(Model.AnimationCollection.AllBakedMatrices);
                    //_gl.UniformMatrix4((int)_locBoneMatrices, (uint)modelBakedFrame.BoneTransformations.Length, false,

                    SSBOAnimations = new BufferObject<float>(_gl, span, BufferTargetARB.ShaderStorageBuffer);

                    ++_nAnimations;
                    if (_nAnimations > 5000)
                    {
                        Warning($"Uploaded {_nAnimations} more than 5000 animations.");
                    }
                }

                break;
            default:
                // Might be uniform based.
                break;
        }
        if (_traceAnimations) Trace($"Uploaded Animations");
        _isUploaded = true;
    }


    public void Release()
    {
        _isUploaded = false;
        if (Model != null)
        {
            if (_traceAnimations) Trace($"Releasing Animations");
            if (AnimStrategy == Flags.GLAnimBuffers.AnimSSBO)
            {
                SSBOAnimations!.Dispose();
                SSBOAnimations = null;
                --_nAnimations;
            }
        }
    }


    /**
     * Release the ressources creaeted by Upload again.
     */
    public override void Dispose()
    {
        // TXWTODO: Also free arrays explicitely here?
        if (_isUploaded)
        {
            Release();
        }
    }


    public override bool IsUploaded()
    {
        if (SSBOAnimations != null)
        {
            if (!_isUploaded)
            {
                Error($"Boom");
            }
        }

        return _isUploaded == true;
    }


    public SkAnimationsEntry(GL gl, engine.joyce.Model? jModel, Flags.GLAnimBuffers animStrategy)
        : base(jModel)
    {
        _gl = gl;
        AnimStrategy = animStrategy;
    }
}