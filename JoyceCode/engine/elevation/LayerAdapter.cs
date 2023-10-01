
using System;
using System.Collections.Generic;
using System.Text;

namespace engine.elevation
{
    internal class LayerAdapter : IElevationProvider
    {
        public string layer { get;  }
        
        private Cache _elevationCache;


        public ElevationSegment GetElevationSegmentBelow(in geom.Rect2 rect2)
        {
            return _elevationCache.ElevationCacheGetRectBelow(rect2, layer);
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
