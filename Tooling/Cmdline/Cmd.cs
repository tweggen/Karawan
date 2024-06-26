﻿using Console = System.Console;

class Cmd
{
    static int Main(string[] args)
    {
        Console.Error.WriteLine($"joycecmd: Processing. had {args.Length} arguments.");
        foreach (var str in args)
        {
            Console.Error.WriteLine($"arg: {str}");
        }
        if (args.Length < 1)
        {
            Console.Error.WriteLine("joycecmd requires at least one argument.");
            return 1;

        }

        int result = 0;

        try {
            switch (args[0])
            {
                case "fbx2ascii":
                    result = new CmdLine.Fbx2Ascii(args).Execute();
                    break;
                case "packtextures":
                    result = new CmdLine.PackTextures(args) { Trace = m => Console.Error.WriteLine(m) }.Execute();
                    break;
                case "res2target":
                    result = new CmdLine.Res2Target(args) { Trace = m => Console.Error.WriteLine(m) }.Execute();
                    break;
                default:
                    result = new CmdLine.Help(args).Execute();
                    break;
            }
        } catch (System.Exception e)
        {
            Console.Error.WriteLine($"Error executing command: {e}.");
        }

        return result;
    }
}