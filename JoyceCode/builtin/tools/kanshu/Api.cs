using System;
using System.Collections.Generic;
using static engine.Logger;

namespace builtin.tools.kanshu;

public class Api
{
    public static bool ApplyRuleset(
        Graph graph, 
        List<Rule> rules)
    {
        bool hadMatch = false;
        
        foreach (var rule in rules)
        {
            GraphMatcher gm = new();
            if (gm.FindMatch(graph, rule.Pattern, out Match match))
            {
                match.Rule = rule;
                
            }
            
            // TXWTODO: Continue iteration or restart iteration depending on depth-first or breadth first
        }

        return true;
    }
    
    
    public static void UnitTest()
    {
        try
        {
            var graph = Graph.Create(
                new()
                {
                    new() {
                        Label = new Labels( new()
                    {
                        { "type", "Town" }, { "name", "Hannover" }
                    }) },
                    new() {
                        Label = new Labels( new()
                    {
                        { "type", "Town" }, { "name", "Linden" }
                    }) },
                    new() { 
                        Label = new Labels( new()
                    {
                        { "type", "Mall" }, { "name", "Ihmezentrum" }
                    }) },
                    new() {
                        Label = new Labels( new()
                    {
                        { "type", "Mall" }, { "name", "EAG" }
                    }) }
                },
                new()
                {
                    new() { 
                        Label = new ( new()
                        {
                            { "type", "Journey" }, { "kind", "any" }
                        }), 
                        NodeFrom = 0, NodeTo = 1 },
                    new() {
                        Label = new ( new()
                        {
                            { "type", "Journey" }, { "kind", "any" }
                        }), 
                        NodeFrom = 1, NodeTo = 0 },
                    
                    new() {
                        Label = new ( new()
                        {
                            { "type", "Journey" }, { "kind", "by feet" }
                        }),
                        NodeFrom = 1, NodeTo = 2 },
                    new() {
                        Label = new ( new()
                        {
                            { "type", "Journey" }, { "kind", "by feet" }
                        }), 
                        NodeFrom = 2, NodeTo = 1 },
                    
                    new() { 
                        Label = new ( new()
                        {
                            { "type", "Journey" }, { "kind", "by feet" }
                        }),
                        NodeFrom = 0, NodeTo = 3 },
                    new() { 
                        Label = new ( new()
                        {
                            { "type", "Journey" }, { "kind", "by feet" }
                        }), 
                        NodeFrom = 3, NodeTo = 0 },
                    
                });
            var pattern = Pattern.Create(
                new()
                {
                    new()
                    {
                        Predicate = PropertiesPredicate.Create(new ()
                        {
                            { "type", "Town" }
                        }), 
                        Id = 0
                    },
                    new()
                    {
                        Predicate = PropertiesPredicate.Create(new ()
                        {
                            { "type", "Town" }
                        }), 
                        Id = 1
                    },
                },
                new()
                {
                    new()
                    {
                        Predicate = PropertiesPredicate.Create(new()
                        {
                            { "type", "Journey" }  
                        }),
                        NodeFrom = 0, NodeTo = 1,
                    }
                });

            GraphMatcher gm = new();
            var match = gm.FindMatch(graph, pattern, out var dictFound);
            Trace($"Unit test for graph results: dictFound = {dictFound}");
        }
        catch (Exception e)
        {
            Error($"Exception occured: {e}");
        }

    }
}