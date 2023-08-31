using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static engine.Logger;

namespace engine.meta;

public class ExecParallelNode : AExecNode
{
    public override Task? Execute(Func<object, Task?> op)
    {
        List<Task> tAllChildren = null;
        foreach (AExecNode en in _children)
        {
            Task? tChild = en.Execute(op);
            if (null != tChild)
            {
                if (null == tAllChildren)
                {
                    tAllChildren = new();
                }
                tAllChildren.Add(tChild);
            }
        }

        if (null != tAllChildren)
        {
            Task taskAll = Task.WhenAll(tAllChildren);
            return taskAll;
        }
        else
        {
            // Trace("Optimized");
            return null;
        }
    }

    
    public ExecParallelNode(ExecDesc ed0, ExecScope esParent) : base(ed0, esParent)
    {
        _buildChildren(esParent);
    }
}