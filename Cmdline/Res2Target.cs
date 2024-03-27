using System.Diagnostics.Eventing.Reader;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace CmdLine;

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
        Trace("res2target Execute called");     
        try
        {
			GameConfig gc = new (_args[1]) { Trace=Trace };
            gc.Load();
			#if false
            Console.Error.WriteLine($"res2target: Reading file {_args[1]}...");
            byte[] fileBytes = File.ReadAllBytes(_args[1]);
            Console.Error.WriteLine($"res2target: Writing file {_args[2]}...");
            File.WriteAllBytes(_args[2], fileBytes);
			#endif
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