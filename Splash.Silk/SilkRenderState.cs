using Silk.NET.OpenGL;

namespace Splash.Silk;

public class SilkRenderState
{
    private GL _gl;

    private SkProgramEntry _lastProgramEntry = null;

    
    private void _unloadProgramEntry()
    {
        if (null == _lastProgramEntry)
        {
            return;
        }

        var pe = _lastProgramEntry;
        _lastProgramEntry = null;
        
        // TXWTODO: Why is that? That is wrong.
        _gl.UseProgram(pe.Handle);
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
    }
}