using Karawan.platform.cs1.splash;
using System;
using System.Collections.Generic;
using System.Text;

namespace Karawan.platform.cs1.splash
{
    public class MaterialBatch
    {
        // public Raylib_CsLo.Material Material;
        public RlMaterialEntry RlMaterialEntry;
        public Dictionary<RlMeshEntry, MeshBatch> MeshBatches;

        public MaterialBatch(in RlMaterialEntry rlMaterialEntry)
        {
            RlMaterialEntry = rlMaterialEntry;
            MeshBatches = new();
        }
    }
}
