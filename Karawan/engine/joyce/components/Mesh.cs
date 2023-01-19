using System;
using System.Numerics;
using System.Collections;

namespace Karawan.engine.joyce.components
{
    struct Mesh
    {
        // TXWTODO: Come up with a supersmart concept only storing the mesh source/factory
        // TXWTODO: Let it use the IList interface
        public IList Vertices;
        public IList Indices;
        public IList UVs;

        public Mesh( IList vertices, IList indices, IList uvs )
        {
            Vertices = vertices;
            Indices = indices;
            UVs = uvs;
        }

        public static Mesh CreateArrayListInstance()
        {
            return new Mesh(new ArrayList(), new ArrayList(), new ArrayList() );
        }
    }
}
