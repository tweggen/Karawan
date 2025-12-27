using System;
using System.Diagnostics;

namespace engine.scheduler;

public class Scheduler
{
    public const int PrioHighest = 0;
    public const int PrioLowest = 20;
    public const int MaxActionsHighest = 100;
    public const int MaxActionsLowest = 1000;

    public const int NPrios = PrioLowest - PrioHighest + 1;

    private WorkerQueue[] _workerQueues;

    public void Enqueue(in Action action, int priority)
    {
        Debug.Assert(priority > 0 && priority < NPrios);
        _workerQueues[priority].Enqueue(action);
    }

    public float RunPart(float dt)
    {
        float usedTime = 0;
        
        int currentPrio = PrioHighest;
        while (currentPrio <= PrioLowest)
        {
            var currentQueue = _workerQueues[currentPrio];
            if (currentQueue.IsEmpty())
            {
                ++currentPrio;
                continue;
            }

            float usedNow = currentQueue.RunPart(dt);
            usedTime += usedNow;
            
            if (usedTime >= dt)
            {
                break;
            }
        }

        return usedTime;
    }
    
    public Scheduler(string name)
    {
        _workerQueues = new WorkerQueue[NPrios];
        for (int i = 0; i < NPrios; i++)
        {
            _workerQueues[i] = new($"{name}_{i}");
        }
    }
}