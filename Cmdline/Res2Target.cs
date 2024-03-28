using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace CmdLine
{

    public class Res2Target
    {
        private string[] _args;
        public Action<string> Trace = (msg) => System.Diagnostics.Debug.WriteLine(msg);

        public void Help()
        {
            Trace("res2target <gamejson>");
        }

        public int Execute()
        {
            Trace("res2target: Working...");
            try
            {
                GameConfig gc = new GameConfig(_args[1]) { Trace = Trace };
                gc.Load();
                Trace($"res2target: Writing android assets...");
                AndroidResourceWriter arw = new AndroidResourceWriter()
                {
                    MapResources = gc.MapResources,
                    Trace = Trace,
                    DestinationPath = "./AndroidResources.xml"
                };
                arw.Execute();
                Trace($"res2target: Done.");
            }
            catch (Exception e)
            {
                Trace($"Exception in Execute: {e}");
            }

            return 0;
        }


        public Res2Target(string[] args)
        {
            if (args.Length != 2)
            {
                throw new ArgumentException();
            }

            _args = args;
        }
    }
}