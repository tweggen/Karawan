using System.Numerics;
using builtin.tools;
using Silk.NET.OpenGL;

namespace Splash.Silk;

public class SilkFrame : IDisposable
{
    public List<IDisposable> ListFrameDisposables = null;
    public RenderFrame RenderFrame;
    public DateTime StartTime;
    public DateTime EndTime;
    private static RunningAverageComputer durationAverage = new();

    private void _disposeDisposables()
    {
        foreach (var disp in ListFrameDisposables)
        {
            disp.Dispose();
        }

        ListFrameDisposables = null;
    }


    private SortedDictionary<int, int> _instanceBufferSizes = new();
    
    public void RegisterInstanceBuffer(in Span<Matrix4x4> span)
    {
        int length = span.Length;
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
        if (null != ListFrameDisposables)
        {
            _disposeDisposables();
        }

        EndTime = DateTime.UtcNow;
        durationAverage.Add((float) (EndTime-StartTime).TotalMicroseconds);
    }


    public SilkFrame(GL gl, RenderFrame renderFrame)
    {
        RenderFrame = renderFrame;
        ListFrameDisposables = new();
        StartTime = DateTime.UtcNow;
    }
    
}