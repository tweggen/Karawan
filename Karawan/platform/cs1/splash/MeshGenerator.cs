using System;
using System.Numerics;

namespace Karawan.platform.cs1.splash
{
    public unsafe class MeshGenerator
    {
        public unsafe static void AllocateRaylibMesh( Raylib_CsLo.Mesh *rlm, int nVertices, int nIndices )
        {
            rlm->vertexCount = nVertices;
            rlm->triangleCount = nIndices / 3;
            rlm->vertices = (float*)Raylib_CsLo.Raylib.MemAlloc((uint)(rlm->vertexCount * 3 * sizeof(float)));
            rlm->texcoords = (float*)Raylib_CsLo.Raylib.MemAlloc((uint)(rlm->vertexCount * 2 * sizeof(float)));
            rlm->normals = (float*)Raylib_CsLo.Raylib.MemAlloc((uint)(rlm->vertexCount * 3 * sizeof(float)));
            rlm->indices = (ushort*)Raylib_CsLo.Raylib.MemAlloc((uint)(rlm->triangleCount * 3 * sizeof(ushort)));
        }



        public static Raylib_CsLo.Mesh CreateRaylibMesh( in engine.joyce.components.Mesh mesh )
        {
            if( null==mesh.Normals )
            {
                //mesh.GenerateNormals();
            }
            Raylib_CsLo.Mesh rlm = new();

            var nVertices = mesh.Vertices.Count;
            var nIndices = mesh.Indices.Count;
            {
                var nUVs = mesh.UVs.Count;
                if (nUVs != nVertices)
                {
                    // TXWTODO: Throw an exception
                    System.Console.WriteLine("Problem");
                }
            }

            AllocateRaylibMesh(&rlm, nVertices, nIndices);
            for(int v=0; v<nVertices; v++)
            {
                Vector3 vertex = (Vector3) mesh.Vertices[v];
                Vector2 uv = (Vector2)mesh.UVs[v];
                rlm.vertices[v * 3 + 0] = vertex.X;
                rlm.vertices[v * 3 + 1] = vertex.Y;
                rlm.vertices[v * 3 + 2] = vertex.Z;
                rlm.texcoords[v * 2 + 0] = uv.X;
                rlm.texcoords[v * 2 + 1] = uv.Y;
            }
            for(int i=0;i<nIndices;++i)
            {
                /*
                 * As we just copy, we can ignore the fact these are triangles.
                 */
                rlm.indices[i] = (ushort)(int)mesh.Indices[i];
            }

            return rlm;
        }
    }
}
