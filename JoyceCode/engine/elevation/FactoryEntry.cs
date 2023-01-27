
using System;
using System.Collections.Generic;
using System.Text;

namespace engine.elevation
{
    using ElevationEntryFactoryFunction =
        Func<Object, IElevationProvider, int, int, CacheEntry>;

    internal class FactoryEntry
    {

        public Object context;
        public string layer;
        public ElevationEntryFactoryFunction factoryFunction;
        public IOperator elevationOperator;

        public FactoryEntry (
            Object context0,
            string layer0, 
            ElevationEntryFactoryFunction factoryFunction0,
            engine.elevation.IOperator elevationOperator0
        ) {
            context = context0;
            layer = layer0;
            elevationOperator = elevationOperator0;
            factoryFunction = factoryFunction0;
        }

    }


}
}
