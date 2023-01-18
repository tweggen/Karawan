using System;
using System.Numerics;
using System.Collections;

namespace Karawan.engine.joyce.components
{
    class Mesh1
    {
        // TXWTODO: Come up with a supersmart concept only storing the mesh source/factory
        // TXWTODO: Let it use the IList interface
        public ArrayList Vertices;
        public ArrayList Indices;
        public ArrayList UVs;
    }
}
