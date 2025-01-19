using System;
using System.Collections.Generic;
using static engine.Logger;

namespace builtin.tools.kanshu;

public class Api
{
    public static void UnitTest()
    {
        try
        {
            Graph<string, string> graph = Graph<string, string>.Create(
                new()
                {
                    new() { Label = "Town" },
                    new() { Label = "Town" },
                    new() { Label = "Town" }
                },
                new()
                {
                    new() { Label = "Any Journey", NodeFrom = 0, NodeTo = 1 },
                    new() { Label = "Any Journey", NodeFrom = 1, NodeTo = 0 },
                    new() { Label = "By Foot", NodeFrom = 0, NodeTo = 2 },
                    new() { Label = "By Foot", NodeFrom = 2, NodeTo = 0 },
                    new() { Label = "By Foot", NodeFrom = 1, NodeTo = 2 },
                    new() { Label = "By Foot", NodeFrom = 2, NodeTo = 1 },
                });
            Pattern<string, string> pattern = Pattern<string, string>.Create(
                new()
                {
                    new()
                    {
                        Label = "Town", Id = 0
                    },
                    new()
                    {
                        Label = "Town", Id = 1
                    }
                },
                new()
                {
                    new()
                    {
                        Label = "Any Journey", NodeFrom = 0, NodeTo = 1
                    }
                });

            GraphMatcher<string, string> gm = new();
            gm.FindMatch(graph, pattern, out var dictFound);
            Trace($"Unit test for graph results: dictFound = {dictFound}");
        }
        catch (Exception e)
        {
            Error($"Exception occured: {e}");
        }

    }
}