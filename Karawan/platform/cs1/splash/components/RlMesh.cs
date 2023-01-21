using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Karawan.platform.cs1.splash.components
{

    struct RlMesh
    {
        public Raylib_CsLo.Mesh Mesh;
        public Raylib_CsLo.Material Material;

        public RlMesh(
            in Raylib_CsLo.Mesh rlMesh,
            in Raylib_CsLo.Material rlMaterial
        )
        {
            Mesh = rlMesh;
            Material = rlMaterial;
        }
    };

}