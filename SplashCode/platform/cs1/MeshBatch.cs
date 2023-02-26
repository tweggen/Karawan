using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Karawan.platform.cs1
{
    public class MeshBatch
    {
        // public Raylib_CsLo.Mesh Mesh;
        public List<Matrix4x4> Matrices;

        public MeshBatch()
        {
            Matrices = new();
        }
    }
}
