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
            var graph = Graph<Properties, Properties>.Create(
                new()
                {
                    new() {
                        Label = new Properties( new()
                    {
                        { "type", "Town" }, { "name", "Hannover" }
                    }) },
                    new() {
                        Label = new Properties( new()
                    {
                        { "type", "Town" }, { "name", "Linden" }
                    }) },
                    new() { 
                        Label = new Properties( new()
                    {
                        { "type", "Mall" }, { "name", "Ihmezentrum" }
                    }) },
                    new() {
                        Label = new Properties( new()
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
            var pattern = Pattern<Properties,Properties>.Create(
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

            GraphMatcher<Properties, Properties> gm = new();
            gm.FindMatch(graph, pattern, out var dictFound);
            Trace($"Unit test for graph results: dictFound = {dictFound}");
        }
        catch (Exception e)
        {
            Error($"Exception occured: {e}");
        }

    }
}