using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace engine;

using TaskArg = Func<Task<int>>; 

internal class PrioQueue
{
    public int Thread;
    public int Prio;
    public Queue<TaskArg> Queue;
}


public class RoundRobin
{
    private PrioQueue[] PrioQueues;
    public void QueueTask(int prio, int thread, TaskArg func)
    {
        
    }


    public RoundRobin()
    {
    }
}