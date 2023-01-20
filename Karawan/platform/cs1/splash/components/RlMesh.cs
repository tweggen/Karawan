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

        public RlMesh(Raylib_CsLo.Mesh rlMesh)
        {
            Mesh = rlMesh;
        }
    };

}