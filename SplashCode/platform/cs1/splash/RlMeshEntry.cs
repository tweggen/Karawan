using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Karawan.platform.cs1.splash
{
    public class RlMeshEntry
    {
        public engine.joyce.Mesh JMesh;
        public Raylib_CsLo.Mesh RlMesh;

        public bool IsMeshUploaded()
        {
            return RlMesh.vaoId != 0;
        }

        public RlMeshEntry(engine.joyce.Mesh jMesh)
        {
            JMesh = jMesh;
            RlMesh.vaoId = 0;
        }
    }
}
