using System.Collections.Generic;

namespace builtin.tools;

public class RunningAverageComputer
{
    private object _lo = new();
    private Queue<float> _queueSamples = new();
    private float _accumulator = 0f;
    private int _nSamples = 10;

    public void Add(float sample)
    {
        lock (_lo)
        {
            while(_queueSamples.Count >= _nSamples)
            {
                _accumulator -= _queueSamples.Dequeue();
            }
            _queueSamples.Enqueue(sample);
            _accumulator += sample;
        }
    }


    public float GetRunningAverage()
    {
        lock (_lo)
        {
            if (0 == _queueSamples.Count)
            {
                return 0f;
            }

            return _accumulator / (_nSamples);
        }
    }

    public void Reset()
    {
        lock (_lo)
        {
            _queueSamples.Clear();
            _accumulator = 0f;
            _nSamples = 0;
        }
    }
}