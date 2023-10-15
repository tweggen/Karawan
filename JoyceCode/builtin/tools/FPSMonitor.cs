using System.Diagnostics;
using static engine.Logger;

namespace builtin.tools;

public class FPSMonitor
{
    private object _lo = new();

    private string _name;
    private builtin.tools.RunningAverageComputer _fpsCounter = new();
    
    private long _previousSeconds = 0;
    private void _updateFpsAverage()
    {
        float fps = 0f;
        /*
         * Do these updates every second
         */
        lock(_lo) {
            long seconds = Stopwatch.GetTimestamp() / Stopwatch.Frequency;
            if (_previousSeconds != seconds)
            {

                float dt;
                lock (_lo)
                {
                    dt = _fpsCounter.GetRunningAverage();
                }

                if (0 == dt)
                {
                    fps = 0f;
                }
                else
                {
                    fps = 1f / dt;
                }
            }

            _previousSeconds = seconds;
        }

        if (0f != fps)
        {
            Trace($"{_name} #fps {fps}");
        }
    }


    public void Update()
    {
        _updateFpsAverage();
    }
    

    public void OnFrame(float dt)
    {
        _fpsCounter.Add(dt);
    }

    public FPSMonitor(string name)
    {
        _name = name;
    }
}