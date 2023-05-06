using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using ObjLoader.Loader.Loaders;
using engine.joyce;

namespace Joyce.builtin.loader;

public class Obj
{
    static private object _lo = new();
    static private ObjLoaderFactory _objLoaderFactory = new();

    private static ulong _toHash(int vertexIndex, int textureIndex, int normalIndex)
    {
        return
            (((ulong)vertexIndex) & 0x001fffff)
            | ((((ulong)vertexIndex) & 0x001fffff) << 21)
            | ((((ulong)vertexIndex) & 0x001fffff) << 42)
            ;
    }
    
    static async Task<InstanceDesc> LoadModelInstance(string url)
    {
        InstanceDesc jInstanceDesc = new();
        var objLoader = _objLoaderFactory.Create();
        var fileStream = new FileStream(url, FileMode.Open, FileAccess.Read);
        var loadedObject = objLoader.Load(fileStream);
        uint[] tri = new uint[3];
        
        foreach (var loadedMaterial in loadedObject.Materials)
        {
        }

        foreach (var loadedGroup in loadedObject.Groups)
        {
            /*
             * For each of the Wavefront groups we create a mesh
             */
            engine.joyce.Mesh jMesh = engine.joyce.Mesh.CreateListInstance();

            /*
             * We map each distinct triplet of indices to an index
             * in our table.
             */
            SortedDictionary<ulong, uint> mapIndices = new();
            foreach (var loadedFace in loadedGroup.Faces)
            {
                for (int idx=0; idx<3; ++idx)
                {
                    int vertexIndex = loadedFace[idx].VertexIndex;
                    int textureIndex = loadedFace[idx].TextureIndex;
                    int normalIndex = loadedFace[idx].NormalIndex;
                    ulong hash = _toHash( vertexIndex, textureIndex, normalIndex);
                    uint myIndex = 0;
                    if (mapIndices.ContainsKey(hash))
                    {
                        myIndex = mapIndices[hash];
                    }
                    else
                    {
                        jMesh.p(
                            loadedObject.Vertices[vertexIndex].X,
                            loadedObject.Vertices[vertexIndex].Y,
                            loadedObject.Vertices[vertexIndex].Z
                            );
                        jMesh.UV(
                            loadedObject.Textures[textureIndex].X,
                            loadedObject.Textures[textureIndex].Y
                            );
                        jMesh.N(
                            loadedObject.Normals[normalIndex].X,
                            loadedObject.Normals[normalIndex].Y,
                            loadedObject.Normals[normalIndex].Z
                            );
                        myIndex = (uint) jMesh.WriteIndexVertices;
                    }

                    tri[idx] = myIndex;
                }
                jMesh.Idx(tri[0], tri[1], tri[2]);
            }
        }
        return jInstanceDesc;
    }   
}