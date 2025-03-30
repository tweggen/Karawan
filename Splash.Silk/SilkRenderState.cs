using Silk.NET.OpenGL;

namespace Splash.Silk;

public class SilkRenderState
{
    private GL _gl;

    private SkProgramEntry _lastProgramEntry = null;

    public SilkTextureChannelState Texture0;
    public SilkTextureChannelState Texture2;

    public BufferObject<float>? BoneMatrices;

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
        
        // TXWTODO: Why is that? That is wrong.
        _gl.UseProgram(pe.Handle);
    }

    
    public void UseBoneMatrices(BufferObject<float>? boneMatrices)
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