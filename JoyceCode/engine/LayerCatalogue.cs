using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text.Json;
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
    
    
    public void LoadConfig(JsonElement jeConfigLayers)
    {
        try
        {
            foreach (var pair in jeConfigLayers.EnumerateObject())
            {
                var layerName = pair.Name;
                double zOrder = 0f;

                try
                {

                    if (pair.Value.TryGetProperty("zOrder", out var jeZOrder) && jeZOrder.TryGetDouble(out zOrder))
                    {
                        //
                    }


                    LayerDefinition ld = new()
                    {
                        Name = layerName, ZOrder = (float)zOrder
                    };

                    Add(ld);
                }
                catch (Exception e)
                {
                    Warning($"Error setting map provider {pair.Name}: {e}");
                }
            }
        }
        catch (Exception e)
        {
            Warning($"Error reading map provider: {e}");
        }
    }
}