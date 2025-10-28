using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace CmdLine
{
    public class CompileAssets
    {
        public Action<string> Trace = (msg) => System.Diagnostics.Debug.WriteLine(msg);
        private string[] _args;
        private Packer _packer;
        public string CurrentPath = "";

        public int Execute()
        {
            Trace("compileassets: Working...");
            GameConfig gc = new GameConfig(Path.Combine(CurrentPath, _args[1])) { Trace = Trace, CurrentPath = CurrentPath };
            gc.Load();

            // _generateTraditional(gc);
            // _generateFromTextureSection(gc);
            Trace($"compileassets: Done");
            return 0;
        }

        
        /**
         * - args[0]: My own name
         * - args[1]: game config path
         * - args[2]: Destination file
         * - args[3]: Current Path (by framework)
         */
        public CompileAssets(string[] args)
        {
            if (args.Length < 3)
            {
                throw new ArgumentException();
            }

            if (args.Length == 4)
            {
                CurrentPath = args[3];
            }

            _args = args;
        }

    }
}