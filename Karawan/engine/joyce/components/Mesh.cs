using System;
using System.Numerics;
using System.Collections;

namespace Karawan.engine.joyce.components
{
    public struct Mesh
    {
        // TXWTODO: Come up with a supersmart concept only storing the mesh source/factory
        // TXWTODO: Let it use the IList interface
        public IList Vertices;
        public IList Indices;
        public IList UVs;
        public IList Normals;

        /**
         * Generate smoothed normals for this mesh.
         */
        public void GenerateNormals()
        {
            if( null != Normals )
            {
                // TXWTODO: Throw
                return;
            }
            var nVertices = Vertices.Count;
            /*
             * These are the normals we create. In the beginning,
             * all of the normals are null.
             */
            Normals = new Vector3[nVertices];

            /*
             * We most likely sum more than one normal.
             * Store the count of normal vectors we added.
             * 
             * TXWTODO: Don't we want to normalize them anyway?
             */
            var normalCount = new int[Vertices.Count];

            var nIndices= Indices.Count;
            /*
             * 
             */
            for( int i=0; i<nIndices; i+=3 )
            {
                /*
                 * Let's assume clockwise triangles.
                 */
                var v0 = (Vector3)Vertices[(int)Indices[i + 0]];
                var v1 = (Vector3)Vertices[(int)Indices[i + 1]];
                var v2 = (Vector3)Vertices[(int)Indices[i + 2]];

                v2 -= v0;
                v1 -= v0;

                var vn = Vector3.Cross(v2, v1);

                for( int j=0; j<3; ++j)
                {
                    int idx = (int) Indices[i + j];
                    if (null == Normals[idx])
                    {
                        Normals[idx] = vn;
                        normalCount[idx] = 1;
                    }
                    else
                    {
                        Normals[idx] = ((Vector3)Normals[idx]) + vn;
                        normalCount[idx]++;
                    }
                }
            }
            for( int n=0; n<nVertices; ++n)
            {
                if( null==Normals[n])
                {
                    // Normal is not referenced, ignore it.
                } else
                {
                    int nNormals = normalCount[n];
                    if( nNormals>0 )
                    {
                        var vn = (Vector3) Normals[n];
                        // TXWTODO: Do we really need this we normalize anyway.
                        Normals[n] = vn / nNormals;
                        vn = Vector3.Normalize(vn);
                    }
                }
            }
        }

        public Mesh( IList vertices, IList indices, IList uvs )
        {
            Vertices = vertices;
            Indices = indices;
            UVs = uvs;
            Normals = null;
        }

        public static Mesh CreateArrayListInstance()
        {
            return new Mesh(new ArrayList(), new ArrayList(), new ArrayList() );
        }
    }
}
