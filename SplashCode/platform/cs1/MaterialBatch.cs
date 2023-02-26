using Karawan.platform.cs1.splash;
using System;
using System.Collections.Generic;
using System.Text;

namespace Karawan.platform.cs1
{
    public class MaterialBatch
    {
        // public Raylib_CsLo.Material Material;
        public Dictionary<RlMeshEntry, MeshBatch> MeshBatches;

        public MaterialBatch()
        {
            MeshBatches = new();
        }
    }
}
