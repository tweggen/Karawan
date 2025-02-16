using System;
using System.Collections.Generic;
using System.Text.Json;
using static engine.Logger;

namespace builtin.tools.kanshu;

public class Api
{
    public static void ApplyRuleset(
        Graph graph, 
        List<Rule> rules)
    {
        foreach (var rule in rules)
        {
            GraphMatcher gm = new(graph, rule);
            var matchResult = gm.FindMatch();
            if (null != matchResult)
            {
                Trace($"rule matched with match {JsonSerializer.Serialize(matchResult)}");
            }
        }
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

            Rule ruleTest = new()
            {
                Name = "RuleTest",
                Probability = 1.0f,
                Condition = Rule.Always,
                Pattern = Pattern.Create(
                    new()
                    {
                        new()
                        {
                            Predicate = LabelsPredicate.Create(new ()
                            {
                                { "type", "Town" }
                            }), 
                            Id = 0
                        },
                        new()
                        {
                            Predicate = LabelsPredicate.Create(new ()
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
                            Predicate = LabelsPredicate.Create(new()
                            {
                                { "type", "Journey" }  
                            }),
                            NodeFrom = 0, NodeTo = 1,
                        }
                    }),
                Replacement = (labels) => default
            };

            ApplyRuleset(graph, new() { ruleTest });
        }
        catch (Exception e)
        {
            Error($"Exception occured: {e}");
        }

    }
}