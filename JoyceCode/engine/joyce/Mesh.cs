using System;
using System.Numerics;
using System.Collections;
using Android.Net.Wifi.Aware;

namespace engine.joyce
{
    public class Mesh
    {
        public int WriteIndexVertices;
        public int WriteIndexIndices;
        public int WriteIndexUVs;
        public int WriteIndexNormals;

        // TXWTODO: Come up with a supersmart concept only storing the mesh source/factory

        /**
         * Indexable array like of Vector3
         */
        public IList Vertices;

        /**
         * Indexable array like of int
         */
        public IList Indices;

        /**
         * Indexable array like of Vector2
         */
        public IList UVs;

        /**
         * Indexable array like of Vector3 or null.
         */
        public IList Normals;

        /**
         * Generate smoothed normals for this mesh.
         */
        public void GenerateCCWNormals()
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

                var vn = Vector3.Cross(v1, v2);

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


        public void p(in Vector3 p)
        {
            Vertices[WriteIndexVertices++] = p;
        }


        public void p(float x, float y, float z)
        {
            p(new Vector3(x, y, z));
        }


        public void UV(in Vector2 uv)
        {
            UVs[WriteIndexUVs++] = uv;
        }


        public void UV( float u, float v )
        {
            UV( new Vector2(u, v));
        }


        public void Idx(int a, int b, int c)
        {
            Indices[WriteIndexIndices++] = a;
            Indices[WriteIndexIndices++] = b;
            Indices[WriteIndexIndices++] = c;
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
