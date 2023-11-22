using Console = System.Console;

class CmdLine
{
    static void Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("joycecmd requires at least one argument.");
            return 1;

        }

        switch (args[0])
        {
            case "fbx2ascii":
                new Fbx2Ascii(args).Execute();
                break;
            default:
                Console.Error.WriteLine("Unsupported command {args[0]}.");
                new Help(args).Execute();
                break;
        }
    }
}