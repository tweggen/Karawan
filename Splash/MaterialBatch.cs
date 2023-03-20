using System;
using System.Collections.Generic;

namespace Splash
{
    public class MaterialBatch
    {
        public AMaterialEntry AMaterialEntry;
        public Dictionary<AMeshEntry, MeshBatch> MeshBatches;

        public MaterialBatch(in AMaterialEntry aMaterialEntry)
        {
            AMaterialEntry = aMaterialEntry;
            MeshBatches = new();
        }
    }
}
