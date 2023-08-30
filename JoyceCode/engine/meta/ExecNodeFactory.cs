using System;
using static engine.Logger;

namespace engine.meta;

public class ExecNodeFactory
{
    static public AExecNode CreateExecNode(ExecDesc ed, ExecScope esParent)
    {
        switch (ed.Mode)
        {
            default:
            case ExecDesc.ExecMode.Constant:
                ErrorThrow($"ExecMode {ed.Mode} is not supported.", m => new ArgumentException(m));
                // This line never is executed, it pleases the compiler.
                return new ExecTaskNode(ed, esParent);
            case ExecDesc.ExecMode.Task:
                return new ExecTaskNode(ed, esParent);
            case ExecDesc.ExecMode.Parallel:
                return new ExecParallelNode(ed, esParent);
            case ExecDesc.ExecMode.ApplyParallel:
                return new ExecApplyParallelNode(ed, esParent);
            case ExecDesc.ExecMode.Sequence:
                return new ExecSequenceNode(ed, esParent);
        }
    }
}