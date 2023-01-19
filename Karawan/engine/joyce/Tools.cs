using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Karawan.engine.joyce
{
    class Tools
    {
        public static void AddCubeMesh( DefaultEcs.Entity entity )
        {
            entity.Set<components.Mesh>( mesh.Tools.CreateCubeMesh() );
        }
    }
}
