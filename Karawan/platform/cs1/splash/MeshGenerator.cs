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
            /*
             * We don't use it currently, but the shader needs it.
             */
            // rlm->colors = (byte*)Raylib_CsLo.Raylib.MemAlloc((uint)(rlm->vertexCount * 4 * sizeof(byte)));
        }



        public static unsafe void CreateRaylibMesh( in engine.joyce.Mesh mesh, out RlMeshEntry rlMeshEntry )
        {
            if( null==mesh.Normals )
            {
                //mesh.GenerateNormals();
            }
            rlMeshEntry = new();

            var nVertices = mesh.Vertices.Count;
            var nIndices = mesh.Indices.Count;
            {
                var nUVs = mesh.UVs.Count;
                if (nUVs != nVertices)
                {
                    // TXWTODO: Throw an exception
                    System.Console.WriteLine("Problem");
                    rlMeshEntry = new();
                    return;
                }
            }
            fixed (Raylib_CsLo.Mesh *prlm = &rlMeshEntry.RlMesh)
            {
                AllocateRaylibMesh(prlm, nVertices, nIndices);
            }
            for(int v=0; v<nVertices; v++)
            {
                Vector3 vertex = (Vector3) mesh.Vertices[v];
                Vector2 uv = (Vector2)mesh.UVs[v];
                rlMeshEntry.RlMesh.vertices[v * 3 + 0] = vertex.X;
                rlMeshEntry.RlMesh.vertices[v * 3 + 1] = vertex.Y;
                rlMeshEntry.RlMesh.vertices[v * 3 + 2] = vertex.Z;
                rlMeshEntry.RlMesh.texcoords[v * 2 + 0] = uv.X;
                rlMeshEntry.RlMesh.texcoords[v * 2 + 1] = uv.Y;
#if false
                rlMeshEntry.RlMesh.colors[v * 4 + 0] = 0x00;
                rlMeshEntry.RlMesh.colors[v * 4 + 1] = 0xff;
                rlMeshEntry.RlMesh.colors[v * 4 + 2] = 0xff;
                rlMeshEntry.RlMesh.colors[v * 4 + 3] = 0x00;
#endif
            }
            for (int i=0;i<nIndices;++i)
            {
                /*
                 * As we just copy, we can ignore the fact these are triangles.
                 */
                rlMeshEntry.RlMesh.indices[i] = (ushort)(int)mesh.Indices[i];
            }
        }
    }
}
