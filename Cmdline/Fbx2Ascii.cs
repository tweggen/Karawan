using System.Diagnostics;
// using UkooLabs.FbxSharpie;

namespace CmdLine;

public class Fbx2Ascii
{
    private string[] _args;
    
    public void Help()
    {
        Console.Error.WriteLine("fbx2ascii <source file> <dest file>");     
    }

    public int Execute()
    {
        try
        {
            Console.Error.WriteLine($"fbx2ascii: Reading file {_args[1]}...");
            //var reader = null; // new FbxBinaryReader(new FileStream(_args[1], FileMode.Open));
            Console.Error.WriteLine($"fbx2ascii: Understanding file {_args[1]}...");
            //var doc = reader.Read();
            Console.Error.WriteLine($"fbx2ascii: Writing file {_args[2]}...");
            //FbxIO.WriteAscii(doc, _args[2]);
            Console.Error.WriteLine($"fbx2ascii: Done.");
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"fbx2ascii: Exception converting {_args[1]}: {e}.");
        }

        return 0;
    }
    
    
    public Fbx2Ascii(string[] args)
    {
        if (args.Length != 3)
        {
            Console.Error.WriteLine("Source and destination file arguments expected.");
            Help();
            throw new ArgumentException();
        }

        _args = args;
    }
}