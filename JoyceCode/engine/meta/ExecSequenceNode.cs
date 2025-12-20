using System;
using System.Threading.Tasks;
using static engine.Logger;

namespace engine.meta;

public class ExecSequenceNode : AExecNode
{
    public override Task? Execute(Func<object, Task?> op)
    {
        // TXWTODO: IS there any way to remove this engine reference?
        return I.Get<Engine>().Run(async () =>
        {
            if (null != _children)
            {
                foreach (AExecNode execNode in _children)
                {
                    Trace($"Starting execNode {execNode.ExecDesc.Comment}");
                    Task? t = execNode.Execute(op);
                    if (null != t)
                    {
                        await t;
                        Trace($"Done with execNode {execNode.ExecDesc.Comment}");
                    }
                }
            }
        });
    }
    
    
    public ExecSequenceNode(ExecDesc ed0, ExecScope esParent) : base(ed0, esParent)
    {
        _buildChildren(esParent);
    }
}
