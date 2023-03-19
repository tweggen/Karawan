using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Karawan.platform.cs1.splash
{
    public class RlMeshEntry : AMeshEntry
    {
        public Raylib_CsLo.Mesh RlMesh;

        public override bool IsMeshUploaded()
        {
            return RlMesh.vaoId != 0;
        }

        public RlMeshEntry(engine.joyce.Mesh jMesh)
            : base(jMesh)
        {
            RlMesh.vaoId = 0;
        }
    }
}