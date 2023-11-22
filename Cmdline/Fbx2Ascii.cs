using UkooLabs.FbxSharpie;

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
        var reader = new FbxBinaryReader(new FileStream(_args[0], FileMode.Open));
        var doc = reader.Read();
        FbxIO.WriteAscii(doc, _args[1]);
        return 0;
    }
    
    
    public Fbx2Ascii(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("Source and destination file arguments expected.");
            Help();
            throw new ArgumentException();
        }
    }
}