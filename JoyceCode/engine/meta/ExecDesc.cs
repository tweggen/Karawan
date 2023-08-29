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
        ApplyParallel,
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

    /**
     * What list shall we apply?
     */
    public string Selector { get; set; }

    /**
     * And to what parameter shall we apply?
     */
    public string Target { get; set; }
    
    public List<ExecDesc> Children { get; set; }

    public string Implementation { get; set; }
    
    /**
     * Parameters overriding the global parameters and the
     * parameters resulting from apply operations.
     */
    public Dictionary<string, string> Parameters { get; set; }
}
