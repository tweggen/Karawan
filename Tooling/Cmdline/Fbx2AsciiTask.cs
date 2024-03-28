using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace CmdLine
{

    public class Fbx2AsciiTask : Microsoft.Build.Utilities.Task
    {
        private string _sourceFbx;
        private string _destinationFbx;


        [Required]
        public string SourceFbx
        {
            get
            {
                return _sourceFbx;
            }
            set
            {
                _sourceFbx = value;
            }
        }


        [Required]
        public string DestinationFbx
        {
            get
            {
                return _destinationFbx;
            }
            set
            {
                _destinationFbx = value;
            }
        }


        public override bool Execute()
        {
            // Log a high-importance comment
            Log.LogMessage(MessageImportance.High,
                $"Asked to convert {_sourceFbx} to {_destinationFbx}.");

            string[] args = new string[3];
            args[0] = "fbx2ascii";
            args[1] = _sourceFbx;
            args[2] = _destinationFbx;
            int result = new CmdLine.Fbx2Ascii(args).Execute();
            return 0 == result;
        }
    }
}