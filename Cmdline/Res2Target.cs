namespace CmdLine;

public class Res2Target
{
    private string[] _args;
    
    public void Help()
    {
        Console.Error.WriteLine("res2target <gamejson>");     
    }

    public int Execute()
    {
        try
        {
            Console.Error.WriteLine($"res2target: Reading file {_args[1]}...");
            byte[] fileBytes = File.ReadAllBytes(_args[1]);
            Console.Error.WriteLine($"res2target: Writing file {_args[2]}...");
            File.WriteAllBytes(_args[2], fileBytes);
            Console.Error.WriteLine($"res2target: Done.");
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"res2target: Exception copying {_args[1]}: {e}.");
        }

        return 0;
    }
    
    
    public Res2Target(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("game json rgument expected.");
            Help();
            throw new ArgumentException();
        }

        _args = args;
    }
}