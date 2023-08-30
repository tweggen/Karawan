using System;
using System.Threading.Tasks;
using static engine.Logger;

namespace engine.meta;

public class ExecSequenceNode : AExecNode
{
    public override Task Execute(Func<object, Task> op)
    {
        return Task.Run(async () =>
        {
            if (null != _children)
            {
                foreach (AExecNode execNode in _children)
                {
                    await execNode.Execute(op);
                }
            }
        });
    }
    
    
    public ExecSequenceNode(ExecDesc ed0, ExecScope esParent) : base(ed0, esParent)
    {
        _buildChildren(esParent);
    }
}