using System.Collections.Generic;
using System.Runtime.InteropServices;
using DefaultEcs.Threading;

namespace engine.meta;

public class ExecDesc
{
    public enum ExecMode
    {
        Constant,
        Task,
        Parallel,
        // Repeat,
        // Selector,
        Sequence,
        // Switch,
        // Timed,
    };

    public required ExecMode Mode
    {
        get;
        set;
    }

    public List<ExecDesc> Children { get; set; }

    public string Implementation { get; set; }
    // public Dictionary<string, string> Parameters { get; set; }
}
