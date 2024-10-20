using System.Numerics;
using builtin.tools;
using Silk.NET.OpenGL;

namespace Splash.Silk;

public class SilkFrame : IDisposable
{
    private GL _gl;
    public List<IDisposable> ListFrameDisposables = null;
    public RenderFrame RenderFrame;
    public SkProgramEntry LastProgramEntry = null;
    public DateTime StartTime;
    public DateTime EndTime;
    private static RunningAverageComputer durationAverage = new();

    private void _unloadProgramEntry()
    {
        if (null == LastProgramEntry)
        {
            return;
        }

        var pe = LastProgramEntry;
        LastProgramEntry = null;
        _gl.UseProgram(pe.Handle);
    }


    private void _disposeDisposables()
    {
        foreach (var disp in ListFrameDisposables)
        {
            disp.Dispose();
        }

        ListFrameDisposables = null;
    }


    public void UseProgramEntry(SkProgramEntry sh, Action<SkProgramEntry> firstTimeFunc)
    {
        if (LastProgramEntry == sh) return;

        LastProgramEntry = sh;
        firstTimeFunc(sh);
    }


    private SortedDictionary<int, int> _instanceBufferSizes = new();
    private int _instanceIdentity = 0;
    
    public void RegisterInstanceBuffer(in Span<Matrix4x4> span)
    {
        int length = span.Length;
        if (1 == length)
        {
            if (span[0].IsIdentity)
            {
                _instanceIdentity++;
            }
        }
        if (_instanceBufferSizes.TryGetValue(length, out var value))
        {
            _instanceBufferSizes[length] = value + 1;
        }
        else
        {
            _instanceBufferSizes[length] = 1;
        }
    }
    

    public void Dispose()
    {
        if (null != LastProgramEntry)
        {
            _unloadProgramEntry();
        }
        
        if (null != ListFrameDisposables)
        {
            _disposeDisposables();
        }

        EndTime = DateTime.UtcNow;
        durationAverage.Add((float) (EndTime-StartTime).TotalMicroseconds);
    }


    public SilkFrame(GL gl, RenderFrame renderFrame)
    {
        _gl = gl;
        RenderFrame = renderFrame;
        ListFrameDisposables = new();
        StartTime = DateTime.UtcNow;
    }
    
}