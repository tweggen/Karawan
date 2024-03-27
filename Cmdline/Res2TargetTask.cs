using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace CmdLine;

public class Res2TargetTask : Microsoft.Build.Utilities.Task
{
    private string _sourcePath;
    private string _destinationPath;
    
    
    [Required]
    public string SourcePath
    {
        get
        {
            return _sourcePath;
        }
        set
        {
            _sourcePath = value;
        }
    }
    
    
    [Required]
    public string DestinationPath
    {
        get
        {
            return _destinationPath;
        }
        set
        {
            _destinationPath = value;
        }
    }
    
    
    public override bool Execute()
    {
        // Log a high-importance comment
        Log.LogMessage(MessageImportance.High,
            $"Asked to convert {_sourcePath} to {_destinationPath}.");

        string[] args = new string[3];
        args[0] = "res2target";
        args[1] = _sourcePath;
        args[2] = _destinationPath;
        int result = new CmdLine.Res2Target(args).Execute();
        return 0 == result;
    }
}