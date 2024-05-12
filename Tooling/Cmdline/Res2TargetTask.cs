using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace CmdLine
{
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

        private string _outputDirectory = "";

        public string OutputDirectory
        {
            get
            {
                return _outputDirectory;
            }
            set
            {
                _outputDirectory = value;
            }
        }


        public override bool Execute()
        {
            // Log a high-importance comment
            Log.LogMessage(MessageImportance.High, $"Asked to convert data from \"{_gameJson}\" to \"{OutputDirectory}\".");

            string[] args = new string[4];
            args[0] = "res2target";
            args[1] = _gameJson;
            args[2] = _outputDirectory;
            args[3] = Path.GetFullPath(".");
            int result = new CmdLine.Res2Target(args) { Trace = msg => Log.LogMessage(MessageImportance.High, msg) }.Execute();
            return 0 == result;
        }
    }
}