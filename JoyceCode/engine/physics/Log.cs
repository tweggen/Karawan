using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using BepuPhysics;

namespace engine.physics;


public class Log
{
    private object _lo = new();

    private Queue<actions.ABase> _queueActions = new();

    public JsonSerializerOptions JsonSerializerOptions = new()
    {
        IncludeFields = true
    };

    public void Append(actions.ABase pa)
    {
        lock (_lo)
        {
            _queueActions.Enqueue(pa);
        }
    }


    public JsonNode Dump()
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