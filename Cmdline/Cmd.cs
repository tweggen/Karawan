using Console = System.Console;

class Cmd
{
    static int Main(string[] args)
    {
        Console.Error.WriteLine($"joycecmd invoked with arguemnts {args}");
        Console.Error.WriteLine("joycecmd: Processing.");
        if (args.Length < 1)
        {
            Console.Error.WriteLine("joycecmd requires at least one argument.");
            return 1;

        }

        int result = 0;

        switch (args[0])
        {
            case "fbx2ascii":
                result = new CmdLine.Fbx2Ascii(args).Execute();
                break;
            default:
                Console.Error.WriteLine($"Unsupported command {args[0]}.");
                result = new CmdLine.Help(args).Execute();
                break;
        }

        return result;
    }
}