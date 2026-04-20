using System;
using System.Diagnostics;
using static engine.Logger; 

namespace builtin.tools;

public class SectionMeter : IDisposable
{
    private static readonly engine.Dc _dc = engine.Dc.Tools;

    private Stopwatch _stopwatch = new();
    private string _title;
    
    public void Dispose()
    {
        _stopwatch.Stop();
        Trace(_dc, $"Section {_title} took {_stopwatch.Elapsed.TotalMilliseconds}ms.");
    }

    
    public SectionMeter(string title)
    {
        _title = title;
        _stopwatch.Start();
    }
}