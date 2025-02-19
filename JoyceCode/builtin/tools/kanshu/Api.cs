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
        List<MatchResult> listMatchingResults = new();
        
        foreach (var rule in rules)
        {
            GraphMatcher gm = new(graph, rule);
            var matchResult = gm.FindMatch();
            if (null != matchResult)
            {
                Trace($"rule matched with match {JsonSerializer.Serialize(matchResult)}");
                listMatchingResults.Add(matchResult);
            }
        }
        
        // TXWTODO: Select by probability instead of simply picking the first
        if (listMatchingResults.Count > 0)
        {
            var matchingResult = listMatchingResults[0];
            
            /*
             * Finally, replace the graph we have found by a new one using the
             * previously created bindings.
             */
            Graph? newGraph = matchingResult.Rule.Replacement(graph, matchingResult);
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
                        Labels = Labels.FromStrings( new()
                    {
                        { "type", "Town" }, { "name", "Hannover" }
                    }) },
                    new() {
                        Labels = Labels.FromStrings( new()
                    {
                        { "type", "Town" }, { "name", "Linden" }
                    }) },
                    new() { 
                        Labels = Labels.FromStrings( new()
                    {
                        { "type", "Mall" }, { "name", "Ihmezentrum" }
                    }) },
                    new() {
                        Labels = Labels.FromStrings( new()
                    {
                        { "type", "Mall" }, { "name", "EAG" }
                    }) }
                },
                new()
                {
                    new() { 
                        Labels = Labels.FromStrings( new()
                        {
                            { "type", "Journey" }, { "kind", "any" }
                        }), 
                        NodeFrom = 0, NodeTo = 1 },
                    new() {
                        Labels = Labels.FromStrings( new()
                        {
                            { "type", "Journey" }, { "kind", "any" }
                        }), 
                        NodeFrom = 1, NodeTo = 0 },
                    
                    new() {
                        Labels = Labels.FromStrings ( new()
                        {
                            { "type", "Journey" }, { "kind", "by feet" }
                        }),
                        NodeFrom = 1, NodeTo = 2 },
                    new() {
                        Labels = Labels.FromStrings ( new()
                        {
                            { "type", "Journey" }, { "kind", "by feet" }
                        }), 
                        NodeFrom = 2, NodeTo = 1 },
                    
                    new() { 
                        Labels = Labels.FromStrings ( new()
                        {
                            { "type", "Journey" }, { "kind", "by feet" }
                        }),
                        NodeFrom = 0, NodeTo = 3 },
                    new() { 
                        Labels = Labels.FromStrings ( new()
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
                            Predicate = LabelsPredicate.Create(
                                new()
                                {
                                    { "type", "Journey" }  
                                },
                                new ()
                                {
                                    { "kind", "Kind" }
                                }
                            ),
                            NodeFrom = 0, NodeTo = 1,
                        },
                        new()
                        {
                            Predicate = LabelsPredicate.Create(
                                new()
                                {
                                    { "type", "Journey" }  
                                },
                                new ()
                                {
                                    { "kind", "Kind" }
                                }
                            ),
                            
                            NodeFrom = 1, NodeTo = 0,
                        }
                    }),
                //Replacement = ConstantReplacement.Create()
            };

            ApplyRuleset(graph, new() { ruleTest });
        }
        catch (Exception e)
        {
            Error($"Exception occured: {e}");
        }

    }
}