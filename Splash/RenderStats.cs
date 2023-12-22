using System.Collections.Generic;

namespace Splash;

public class RenderStats
{
    private List<FrameStats> _queueStats = new();
    private int _maxDepth = 10;

    private FrameStats _aver = new();


    public void PushFrame(FrameStats rs)
    {
        while(true)
        {
            int i = _queueStats.Count - 1;
            if (i < _maxDepth) break;
            FrameStats last = _queueStats[i];
            _queueStats.RemoveAt(i);
            _aver -= last;
        }
        _queueStats.Insert(0, rs);        
        _aver += rs;
    }


    public FrameStats GetAverage()
    {
        FrameStats fs = new(_aver);
        fs /= _queueStats.Count;
        return fs;
    }

}