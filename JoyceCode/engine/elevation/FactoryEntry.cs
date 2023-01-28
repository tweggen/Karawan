
using System;
using System.Collections.Generic;
using System.Text;

namespace engine.elevation
{
    using ElevationEntryFactoryFunction =
        Func<Object, IElevationProvider, int, int, CacheEntry>;

    internal class FactoryEntry
    {
        public string Layer;
        public IOperator ElevationOperator;

        public FactoryEntry (
            in string layer0, 
            in engine.elevation.IOperator elevationOperator0
        ) {
            Layer = layer0;
            ElevationOperator = elevationOperator0;
        }

    }
}
