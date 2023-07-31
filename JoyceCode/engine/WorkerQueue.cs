using System;
using System.Collections.Generic;
using static engine.Logger;
using System.Diagnostics;

namespace engine
{

    public class WorkerQueue
    {
        private object _lo = new();
        private string _name = "Unnamed WorkerQueue";

        private Queue<Action> _queueActions = new();
        private Stopwatch _stopwatch = new();


        public bool IsEmpty()
        {
            lock (_lo)
            {
                return _queueActions.Count == 0;
            }
        }
        
        
        public void Enqueue(Action action)
        {
            lock (_lo)
            {
                _queueActions.Enqueue(action);
            }
        }

        public float RunPart(float dt)
        {
            lock (_lo)
            {
                if (_queueActions.Count == 0)
                {
                    return 0f;
                }

                _stopwatch.Reset();
                _stopwatch.Start();
            }

            float usedTime = 0f;
            while (true)
            {
                Action action;
                lock (_lo)
                {
                    usedTime = _stopwatch.ElapsedMilliseconds / 1000f;

                    if (_queueActions.Count == 0)
                    {
                        _stopwatch.Stop();
                        return usedTime;
                    }

                    if( usedTime>dt )
                    {
                        _stopwatch.Stop();
                        int queueLeft = _queueActions.Count;
                        if (0 < queueLeft)
                        {
                            Trace($"Left {queueLeft} actions in queue {_name}");
                        }
                        return usedTime;
                    }

                    action = _queueActions.Dequeue();
                }

                try
                {
                    action();
                }
                catch (Exception e)
                {
                    Warning($"Error executing worker queue {_name} action: {e}");
                }
            }
        }

        public WorkerQueue(in string name)
        {
            _name = name;
        }
    }
}