using System;
using System.Collections.Generic;
using DefaultEcs;
using engine;
using engine.draw;
using engine.world;
using static engine.Logger;

namespace builtin.map;

public class DefaultMapProvider : IMapProvider
{
    private readonly object _lo = new();
    private readonly SortedDictionary<string, IWorldMapProvider> _worldMapLayers = new();
    private bool _haveWorldEntities = false;
    private bool _haveWorldBitmap = false;
    
    public void AddWorldMapLayer(string layerKey, IWorldMapProvider worldMapProvider)
    {
        lock (_lo)
        {
            _worldMapLayers.Add(layerKey, worldMapProvider);
        }
    }


    public void RemoveWorldMapProviderLayer(string layerKey)
    {
        lock (_lo)
        {
            _worldMapLayers.Remove(layerKey);
        }
    }


    private void _callWorldMapProviders(Action<IWorldMapProvider> action)
    {
        /*
         * Do not create any entity.
         */
        SortedDictionary<string, IWorldMapProvider> worldMapLayers = new();
        lock (_lo)
        {
            worldMapLayers = _worldMapLayers;
        }

        foreach (var kvp in worldMapLayers)
        {
            // Trace($"Calling world map layer \"{kvp.Key}\".");
            try
            {
                action(kvp.Value);
            }
            catch (System.Exception e)
            {
                Error($"Error executing world map layer \"{kvp.Key}\": {e}");
            }
        }
    }

    
    public void WorldMapCreateEntities(Entity parentEntity, uint cameraMask)
    {
        lock (_lo)
        {
            if (_haveWorldEntities)
            {
                return;
            }

            _haveWorldEntities = true;
        }

        _callWorldMapProviders((worldMapProvider) =>
        {
            worldMapProvider.WorldMapCreateEntities(parentEntity, cameraMask);
        });
    }

    
    public void WorldMapCreateBitmap(IFramebuffer target)
    {
        lock (_lo)
        {
            if (_haveWorldBitmap)
            {
                return;
            }

            _haveWorldBitmap = true;
        }

        _callWorldMapProviders((worldMapProvider) =>
        {
            worldMapProvider.WorldMapCreateBitmap(target);
        });
    }
    

    public void FragmentMapCreateEntities(Fragment worldFragment, uint cameraMask)
    {
        /*
         * Do not create any specific entities.
         */
    }
}