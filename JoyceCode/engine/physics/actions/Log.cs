using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using BepuPhysics;

namespace engine.physics.actions;


public class Log
{
    private object _lo = new();

    public string DumpPath { get; set; }
    
    private Queue<actions.ABase> _queueActions = new();

    public JsonSerializerOptions JsonSerializerOptions = new()
    {
        IncludeFields = true,
        WriteIndented = true
    };

    public void Append(actions.ABase pa)
    {
        lock (_lo)
        {
            _queueActions.Enqueue(pa);
        }
    }




    public JsonNode DumpToNode()
    {
        JsonNode jnRoot = new JsonObject();
        JsonArray jnArray = new JsonArray();
        jnRoot["version"] = "1.0";
        jnRoot["actions"] = jnArray;
        
        Queue<actions.ABase> queueActions;

        /*
         * First, dequeue the number of actions we want to dequeue.
         */
        lock (_lo)
        {
            queueActions = _queueActions;
            _queueActions = new();
        }
        
        /*
         * Then execute the queue.
         */
        foreach (var pa in queueActions)
        {
            var jnAction = pa.ToJson(this);
            jnArray.Add(jnAction);
        }

        return jnRoot;
    }


    public void Dump()
    {
        string filename = $"joyce-physics-dump-{DateTime.UtcNow.ToString("yyyyMMddHHmmss")}.json";

        JsonNode jn = DumpToNode();
        string jsonString = JsonSerializer.Serialize(jn, JsonSerializerOptions);
        File.WriteAllText(Path.Combine(DumpPath, filename), jsonString);
    }
    

    public void Replay(Simulation simulation)
    {
        Queue<actions.ABase> queueActions;

        /*
         * First, dequeue the number of actions we want to dequeue.
         */
        lock (_lo)
        {
            queueActions = _queueActions;
            _queueActions = new();
        }
        
        /*
         * Then execute the queue.
         */
        foreach (var pa in queueActions)
        {
            pa.Execute(null, simulation);
        }
    }
}