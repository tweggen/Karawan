using System;
using System.Numerics;
using System.Collections;

namespace Karawan.engine.joyce.components
{
    struct Mesh1
    {
        // TXWTODO: Come up with a supersmart concept only storing the mesh source/factory
        // TXWTODO: Let it use the IList interface
        public IList Vertices;
        public IList Indices;
        public IList UVs;

        public Mesh1( IList vertices, IList indices, IList uvs )
        {
            Vertices = vertices;
            Indices = indices;
            UVs = uvs;
        }

        public static Mesh1 CreateArrayListInstance()
        {
            return new Mesh1(new ArrayList(), new ArrayList(), new ArrayList() );
        }
    }
}
