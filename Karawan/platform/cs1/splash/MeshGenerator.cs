using System;
using System.Numerics;

namespace Karawan.platform.cs1.splash
{
    public unsafe class MeshGenerator
    {
        public unsafe static void AllocateRaylibMesh( Raylib_CsLo.Mesh rlm, int nVertices, int nIndices )
        {
            rlm.vertexCount = nVertices;
            rlm.triangleCount = nIndices / 3;
            rlm.vertices = (float*)Raylib_CsLo.Raylib.MemAlloc((uint)(rlm.vertexCount * 3 * sizeof(float)));
            rlm.texcoords = (float*)Raylib_CsLo.Raylib.MemAlloc((uint)(rlm.vertexCount * 2 * sizeof(float)));
            rlm.normals = (float*)Raylib_CsLo.Raylib.MemAlloc((uint)(rlm.vertexCount * 3 * sizeof(float)));
            rlm.indices = (ushort*)Raylib_CsLo.Raylib.MemAlloc((uint)(rlm.triangleCount * 3 * sizeof(ushort)));
        }


#if false

void Smooth(Mesh* mesh, bool enabled = true)
        {
            Vector3[Vector3] linkedNormals;
            size_t idx = 0;

            foreach (tri; mesh.vertices[0..mesh.vertexCount * 3].chunks(9))
    {
                Vector3 v1 = Vector3(tri[0], tri[1], tri[2]);
                Vector3 v2 = Vector3(tri[3], tri[4], tri[5]);
                Vector3 v3 = Vector3(tri[6], tri[7], tri[8]);

                Vector3 normalV1 = Vector3CrossProduct(Vector3Diff(v2, v1), Vector3Diff(v3, v1));
                Vector3 normalV2 = Vector3CrossProduct(Vector3Diff(v3, v2), Vector3Diff(v1, v2));
                Vector3 normalV3 = Vector3CrossProduct(Vector3Diff(v1, v3), Vector3Diff(v2, v3));

                Vector3 sum = Vector3Add(Vector3Add(normalV1, normalV2), normalV3);

                if (enabled)
                {
                    if (v1!in linkedNormals) linkedNormals[v1] = Vector3Zero;
                    if (v2!in linkedNormals) linkedNormals[v2] = Vector3Zero;
                    if (v3!in linkedNormals) linkedNormals[v3] = Vector3Zero;

                    linkedNormals[v1] = Vector3Add(linkedNormals[v1], sum);
                    linkedNormals[v2] = Vector3Add(linkedNormals[v2], sum);
                    linkedNormals[v3] = Vector3Add(linkedNormals[v3], sum);
                }
                else
                {
                    Vector3 nor = Vector3Normalize(sum);

                    foreach (k; 0..3)
            {
                        mesh.normals[idx + k * 3 + 0] = nor.x;
                        mesh.normals[idx + k * 3 + 1] = nor.y;
                        mesh.normals[idx + k * 3 + 2] = nor.z;
                    }

                }

                idx += 9;
            }

            if (enabled)
            {
                idx = 0;
                foreach (v; mesh.vertices[0..mesh.vertexCount * 3].chunks(3))
        {
                    Vector3 cur = Vector3(v[0], v[1], v[2]);
                    Vector3 nor = Vector3Normalize(linkedNormals[cur]);

                    mesh.normals[idx + 0] = nor.x;
                    mesh.normals[idx + 1] = nor.y;
                    mesh.normals[idx + 2] = nor.z;

                    idx += 3;
                }
            }

            rlUpdateMesh(*mesh, 2, mesh.vertexCount);
        }
#endif

        public static Raylib_CsLo.Mesh CreateRaylibMesh( engine.joyce.components.Mesh mesh )
        {
            if( null==mesh.Normals )
            {
                mesh.GenerateNormals();
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

            AllocateRaylibMesh(rlm, nVertices, nIndices);
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
            return rlm;
        }
    }
}
