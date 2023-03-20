using System;
using System.Collections.Generic;

namespace Karawan.platform.cs1.splash
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
