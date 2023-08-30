using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static engine.Logger;

namespace engine.meta;

public class ExecApplyParallelNode : AExecNode
{
    public override Task Execute(Func<object, Task> op)
    {
        // TXWTODO: Create some common abstract parallel execution class, this code is shared with ExecParallelNode
        List<Task> tAllChildren = new();
        foreach (AExecNode en in _children)
        {
            Task tChild = en.Execute(op);
            tChild.Start();
            tAllChildren.Add(tChild);
        }
        Task taskAll = Task.WhenAll(tAllChildren);
        return taskAll;
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