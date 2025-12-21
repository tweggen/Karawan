using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static engine.Logger;
using Trace = System.Diagnostics.Trace;

namespace engine.meta;

public class ExecApplyParallelNode : AExecNode
{
    public override Task? Execute(Func<object, Task?> op)
    {
        // TXWTODO: Create some common abstract parallel execution class, this code is shared with ExecParallelNode
        List<Task> tAllChildren = null;
        foreach (AExecNode en in _children)
        {
            Task? tChild = en.Execute(op);
            if (tChild != null)
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
            //Trace("Optimize2");
            return null;
        }
    }
    

    public ExecApplyParallelNode(ExecDesc ed0, ExecScope esParent) : base(ed0, esParent)
    {
        IEnumerable<object> listToApply = ExecScope.ApplyParameters[ed0.Selector]();

        if (ExecDesc.Children == null || ExecDesc.Children.Count != 1)
        {
            ErrorThrow("Expecting exactly one child, not found.", m => new ArgumentException(m));
        }

        _children = new();
        ExecDesc edChild = ed0.Children[0];
    
        // ✅ FIXED: Create a NEW scope for each child
        foreach (var applyParam in listToApply)
        {
            // Create fresh parameters for THIS child only
            var childParams = new Dictionary<string, object>();
            childParams[ed0.Target] = applyParam;
        
            // Create a NEW ExecScope with its own dictionary
            var esChild = new ExecScope(esParent, childParams);
        
            // Each child gets isolated parameters
            AExecNode enChild = ExecNodeFactory.CreateExecNode(edChild, esChild);
            _children.Add(enChild);
        }
    }
}