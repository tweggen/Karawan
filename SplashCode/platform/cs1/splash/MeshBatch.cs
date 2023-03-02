using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Karawan.platform.cs1.splash
{
    public class MeshBatch
    {
        public readonly RlMeshEntry RlMeshEntry;
        public readonly List<Matrix4x4> Matrices;

        public MeshBatch(RlMeshEntry rlMeshEntry)
        {
            RlMeshEntry = rlMeshEntry;
            Matrices = new();
        }
    }
}
