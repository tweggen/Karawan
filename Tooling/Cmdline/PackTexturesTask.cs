using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace CmdLine
{

    public class PackTexturesTask : Microsoft.Build.Utilities.Task
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

        private string _executable = "";

        public string Executable
        {
            get { return _executable; }
            set { _executable = value; }
        }

        public override bool Execute()
        {
            // Log a high-importance comment
            Log.LogMessage(MessageImportance.High, $"Asked to pack textures...");

            string[] args = new string[3];
            args[0] = "packtextures";
            args[1] = _gameJson;
            args[2] = _outputDirectory;

            using (var process = new Process())
            {
                process.StartInfo.FileName = Executable;
                string argString = "";
                bool isFirst = true;
                foreach (var arg in args)
                {
                    if (!isFirst)
                    {
                        argString += " ";
                    }
                    isFirst = false;

                    argString += arg;
                }

                process.StartInfo.Arguments = argString;

                //process.StartInfo.FileName = @"cmd.exe";
                //process.StartInfo.Arguments = @"/c dir";      // print the current working directory information
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;


                process.OutputDataReceived += (sender, data) =>
                {
                    if (data != null && data.Data != null)
                    {
                        Log.LogMessage(MessageImportance.High, data.Data);
                    }
                };
                process.ErrorDataReceived += (sender, data) =>
                {
                    if (data != null && data.Data != null)
                    {
                        Log.LogMessage(MessageImportance.High, data.Data);
                    }
                };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                var exited = process.WaitForExit(1000 * 10);     // (optional) wait up to 10 seconds
                Console.WriteLine($"exit {exited}");
            }
            
            //int result = new CmdLine.PackTextures(args) { Trace = msg => Log.LogMessage(MessageImportance.High, msg) }.Execute();
            return true;
        }
    }
}