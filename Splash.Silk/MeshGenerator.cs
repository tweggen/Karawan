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
            var mesh = aMeshParams.JMesh;
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
            
            for(int v=0; v<nVertices; v++)
            {
                Vector3 vertex = (Vector3) mesh.Vertices[v];
                Vector2 uv = (Vector2)mesh.UVs[v];
                uv *= aMeshParams.UVScale;
                uv += aMeshParams.UVOffset;
                Vector3 normals = (Vector3)mesh.Normals[v];
                skMeshEntry.Vertices[v * 3 + 0] = vertex.X;
                skMeshEntry.Vertices[v * 3 + 1] = vertex.Y;
                skMeshEntry.Vertices[v * 3 + 2] = vertex.Z;
                skMeshEntry.UVs[v * 2 + 0] = uv.X;
                skMeshEntry.UVs[v * 2 + 1] = uv.Y;
                skMeshEntry.Normals[v * 3 + 0] = normals.X;
                skMeshEntry.Normals[v * 3 + 1] = normals.Y;
                skMeshEntry.Normals[v * 3 + 2] = normals.Z;

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
