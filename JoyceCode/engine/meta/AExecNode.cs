
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace engine.meta;

public abstract class AExecNode
{
    public ExecDesc ExecDesc;
    public ExecScope ExecScope;

    protected List<AExecNode> _children = null;

    /**
     * Return a started instance of the given function.
     */
    public abstract Task? Execute(Func<object, Task?> operation);
    
    
    protected void _buildChildren(ExecScope esParent)
    {
        if (ExecDesc.Children!=null && ExecDesc.Children.Count>0)
        {
            _children = new List<AExecNode>();
            foreach (var ed in ExecDesc.Children)
            {
                _children.Add(ExecNodeFactory.CreateExecNode(ed, esParent));
            }
        }
    }

    
    public AExecNode(ExecDesc ed0, ExecScope esParent)
    {
        ExecDesc = ed0;

        /*
         * merge the parameters and call the method.
         */
        ExecScope = new(esParent, ed0.Parameters);
    }
}