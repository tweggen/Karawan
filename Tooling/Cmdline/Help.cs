using System;

namespace CmdLine
{ 
    public class Help
    {
        public int Execute()
        {
            Console.Error.WriteLine("Usage: joycecmd fbx2ascii <source binary fbx> <dest ascii fbx.>");
            Console.Error.WriteLine("Usage: joycecmd res2target <json game config> <dest path.>");
            Console.Error.WriteLine("Usage: joycecmd packtextures <json game config> <dest path.>");
            return 0;
        }

        public Help(string[] args)
        {
        }
    }
}