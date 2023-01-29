
using System;
using System.Collections.Generic;
using System.Text;

namespace engine.elevation
{
    internal class LayerAdapter : IElevationProvider
    {
        public string layer { get;  }
        
        private Cache _elevationCache;


        public Rect GetElevationRectBelow(
            float x0, float z0,
            float x1, float z1
        ) {
            return _elevationCache.ElevationCacheGetRectBelow(x0, z0, x1, z1, layer);
        }


        public LayerAdapter(
            in Cache elevationCache,
            in string layer0
        ) {
            _elevationCache = elevationCache;
            layer = layer0;
        }
    }
}
