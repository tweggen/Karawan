using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Splash.components
{
    struct PfMesh
    {
        public AMeshEntry MeshEntry;

        public PfMesh(AMeshEntry meshEntry)
        {
            MeshEntry = meshEntry;
        }
    };

}