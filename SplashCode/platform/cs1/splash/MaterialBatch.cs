using Karawan.platform.cs1.splash;
using System;
using System.Collections.Generic;
using System.Text;

namespace Karawan.platform.cs1.splash
{
    public class MaterialBatch
    {
        // public Raylib_CsLo.Material Material;
        public AMaterialEntry AMaterialEntry;
        public Dictionary<AMeshEntry, MeshBatch> MeshBatches;

        public MaterialBatch(in AMaterialEntry aMaterialEntry)
        {
            AMaterialEntry = aMaterialEntry;
            MeshBatches = new();
        }
    }
}
