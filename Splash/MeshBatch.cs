using System;
using System.Collections.Generic;
using System.Numerics;

namespace Splash
{
    public class MeshBatch
    {
        public readonly AMeshEntry AMeshEntry;
        public readonly List<Matrix4x4> Matrices = new();

        public MeshBatch(in AMeshEntry aMeshEntry)
        {
            AMeshEntry = aMeshEntry;
        }
    }
}
