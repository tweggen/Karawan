using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Karawan.platform.cs1.splash.components
{

    struct RlMesh
    {
        public RlMeshEntry MeshEntry;
        public RlMaterialEntry MaterialEntry;

        public RlMesh(
            RlMeshEntry meshEntry,
            RlMaterialEntry materialEntry
        )
        {
            MeshEntry = meshEntry;
            MaterialEntry = materialEntry;
        }
    };

}