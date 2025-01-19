using System.Collections.Generic;

namespace builtin.tools.kanshu;

public class Api
{
    public static void UnitTest()
    {
        Graph<string, string> graph = Graph<string, string>.Create(
            new()
            {
                new() { Label = "Hannover" },
                new () { Label = "Munich" }
            },
            new()
            {
                new() { Label = "Any Journey" },
            });
        Pattern<string,string> pat1 = new()
        {
            Nodes = new()
            {
                new () {
                    Label = "by train", RequiredConnections = new()
                    {
                        
                    }
                }
            }
        };
    }
}