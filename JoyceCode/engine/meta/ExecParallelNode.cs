using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace engine.meta;

public class ExecParallelNode : AExecNode
{
    public override Task Execute(Func<object, Task> op)
    {
        List<Task> tAllChildren = new();
        foreach (AExecNode en in _children)
        {
            Task tChild = en.Execute(op);
            tAllChildren.Add(tChild);
        }
        Task taskAll = Task.WhenAll(tAllChildren);
        taskAll.Start();
        return taskAll;
    }

    
    public ExecParallelNode(ExecDesc ed0, ExecScope esParent) : base(ed0, esParent)
    {
        _buildChildren(esParent);
    }
}