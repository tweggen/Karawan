using System;
using System.Numerics;
using Silk.NET.OpenGL;
using static engine.Logger;

namespace Splash.Silk
{
    public class MeshGenerator
    {
        public static void FillSilkMesh(in SkMeshEntry skMeshEntry)
        {
            var mesh = skMeshEntry.Params.JMesh;
            if( null==mesh.Normals )
            {
                mesh.GenerateCCWNormals();
            }
            var nVertices = mesh.Vertices.Count;
            var nIndices = mesh.Indices.Count;
            {
                var nUVs = mesh.UVs.Count;
                if (nUVs != nVertices)
                {
                    ErrorThrow("the number of uvs does not match the number of vertices.", (m) => new InvalidOperationException(m));
                }
            }
            skMeshEntry.Vertices = new float[nVertices*3];
            skMeshEntry.UVs = new float[nVertices*2];
            skMeshEntry.Indices = new ushort[nIndices];
            skMeshEntry.Normals = new float[nVertices*3];

            bool haveBones = mesh.BoneWeights != null && mesh.BoneIndices != null; 
            if (haveBones)
            {
                skMeshEntry.BoneWeights = new float[nVertices * 4];
                skMeshEntry.BoneIndices = new int[nVertices * 4];
            }

            for (int v = 0; v < nVertices; v++)
            {
                Vector3 vertex = (Vector3)mesh.Vertices[v];
                Vector2 uv = (Vector2)mesh.UVs[v];
                

                uv *= skMeshEntry.Params.UVScale;
                uv += skMeshEntry.Params.UVOffset;
                Vector3 normals = (Vector3)mesh.Normals[v];
                skMeshEntry.Vertices[v * 3 + 0] = vertex.X;
                skMeshEntry.Vertices[v * 3 + 1] = vertex.Y;
                skMeshEntry.Vertices[v * 3 + 2] = vertex.Z;
                skMeshEntry.UVs[v * 2 + 0] = uv.X;
                skMeshEntry.UVs[v * 2 + 1] = uv.Y;
                skMeshEntry.Normals[v * 3 + 0] = normals.X;
                skMeshEntry.Normals[v * 3 + 1] = normals.Y;
                skMeshEntry.Normals[v * 3 + 2] = normals.Z;
                if (haveBones)
                {
                    Vector4 bw = (Vector4)mesh.BoneWeights[v];
                    engine.joyce.Int4 bi = (engine.joyce.Int4)mesh.BoneIndices[v];

                    skMeshEntry.BoneWeights[v * 4 + 0] = bw.X;
                    skMeshEntry.BoneWeights[v * 4 + 1] = bw.Y;
                    skMeshEntry.BoneWeights[v * 4 + 2] = bw.Z;
                    skMeshEntry.BoneWeights[v * 4 + 3] = bw.W;
                    skMeshEntry.BoneIndices[v * 4 + 0] = bi.B0;
                    skMeshEntry.BoneIndices[v * 4 + 1] = bi.B1;
                    skMeshEntry.BoneIndices[v * 4 + 2] = bi.B2;
                    skMeshEntry.BoneIndices[v * 4 + 3] = bi.B3;
                }
            }

            for (int i=0; i<nIndices; ++i)
            {
                /*
                 * As we just copy, we can ignore the fact these are triangles.
                 */
                var index = mesh.Indices[i];
                if (index > 65535)
                {
                    Error($"Invalid mesh index {index}.");
                }

                skMeshEntry.Indices[i] = (ushort)index;
            }
        }
    }
}
