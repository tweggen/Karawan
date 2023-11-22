namespace CmdLine;

public class Help
{
    public int Execute()
    {
        Console.Error.WriteLine("Usage: joycecmd fbx2ascii <source binary fbx> <dest ascii fbx.>");
        return 0;
    }

    public Help(string[] args)
    {
    }
}