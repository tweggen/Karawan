using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace CmdLine;

public class Res2TargetTask : Microsoft.Build.Utilities.Task
{
    private string _gameJson;
    
    [Required]
    public string GameJson
    {
        get
        {
            return _gameJson;
        }
        set
        {
            _gameJson = value;
        }
    }
    
    
    public override bool Execute()
    {
        // Log a high-importance comment
        Log.LogMessage(MessageImportance.High, $"Asked to convert data from {_gameJson}.");

        string[] args = new string[2];
        args[0] = "res2target";
        args[1] = _gameJson;
        int result = new CmdLine.Res2Target(args) { Trace = msg => Log.LogMessage(MessageImportance.High,msg) }.Execute();
        return 0 == result;
    }
}