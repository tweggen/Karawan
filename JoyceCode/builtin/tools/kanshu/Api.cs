namespace builtin.tools.kanshu;

public class Api
{
    public static void UnitTest()
    {
        Pattern<string,string> pat1 = new()
        {
            Nodes = new()
            {
                new () {
                    Label = "initial", RequiredConnections = new()
                }
            }
        };
    }
}