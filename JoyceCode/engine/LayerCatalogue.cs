using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using static engine.Logger;

namespace engine;

public class LayerDefinition
{
    public string Name { get; set; }
    public float ZOrder { get; set; }
}

public class LayerCatalogue : AModule
{
    private object _lo = new();
    private SortedDictionary<string, LayerDefinition> _layerDefinitions = new();

    public void Add(LayerDefinition ld)
    {
        lock (_lo)
        {
            _layerDefinitions[ld.Name] = ld;
        }
    }


    public LayerDefinition Get(string layername)
    {
        lock (_lo)
        {
            return _layerDefinitions[layername];
        }
    }


    private void _loadConfig(JsonNode nodeConfigLayers)
    {
        try
        {
            if (nodeConfigLayers is JsonObject objConfigLayers)
            {
                foreach (var kvp in objConfigLayers)
                {
                    var layerName = kvp.Key;
                    double zOrder = 0f;

                    try
                    {
                        var zOrderNode = kvp.Value?["zOrder"];
                        if (zOrderNode is not null)
                        {
                            // Safely extract as double
                            zOrder = zOrderNode.GetValue<double>();
                        }

                        LayerDefinition ld = new()
                        {
                            Name = layerName,
                            ZOrder = (float)zOrder
                        };

                        Add(ld);
                    }
                    catch (Exception e)
                    {
                        Warning($"Error setting map provider {layerName}: {e}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Warning($"Error reading map provider: {e}");
        }
    }


    private void _whenLoaded(string path, JsonNode? jn)
    {
        if (null != jn)
        {
            _loadConfig(jn);
        }
    }

    public LayerCatalogue()
    {
        I.Get<engine.casette.Loader>().WhenLoaded("layers", _whenLoaded);
    }
}