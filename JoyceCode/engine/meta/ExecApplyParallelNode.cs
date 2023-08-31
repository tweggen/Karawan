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
        IEnumerable<object> listToApply = ExecScope.ApplyParameters[ed0.Selector];

        if (ExecDesc.Children == null || ExecDesc.Children.Count != 1)
        {
            ErrorThrow("Expecting exactly one child, not found.", m => new ArgumentException(m));
        }

        _children = new();

        ExecDesc edChild = ed0.Children[0];
        
        /*
         * Now build the execnodes for the children by applying the parameter list(s).
         * We create an intermediate parent scope that will contain the value of the
         * apply list.
         */
        var pDummyApplyParams = new Dictionary<string, object>();
        pDummyApplyParams[ed0.Target] = 0;

        var esIntermediateParent = new ExecScope(esParent, pDummyApplyParams);
        foreach (var applyParam in listToApply)
        {
            esIntermediateParent.OverallParams[ed0.Target] = applyParam;
            AExecNode enChild = ExecNodeFactory.CreateExecNode(edChild, esIntermediateParent);
            _children.Add(enChild);
        }
    }
}