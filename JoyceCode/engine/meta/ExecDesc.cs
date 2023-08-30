using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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

    public ExecMode Mode
    {
        get;
        set;
    } = ExecMode.Task;

    
    public string Comment { get; set; }
    
    
    /**
     * This props path may be asked to find out if the node shall be executed or not. 
     */
    public string ConfigCondition { get; set; }
    
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
    public Func<object, Task> Operation { get; set; }
    
    /**
     * Parameters overriding the global parameters and the
     * parameters resulting from apply operations.
     */
    public Dictionary<string, object> Parameters { get; set; }
}
