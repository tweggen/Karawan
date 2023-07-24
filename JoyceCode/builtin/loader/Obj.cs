using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using ObjLoader.Loader.Loaders;
using engine.joyce;
using static engine.Logger;
using Material = ObjLoader.Loader.Data.Material;

namespace builtin.loader;

public class ModelInfo
{
    public override string? ToString()
    {
        return $"ModelInfo {{ AABB: {AABB}, Center: {Center} }}";
    }
    public engine.geom.AABB AABB = new();
    public Vector3 Center = new();
}

class AssetMaterialStreamProvider : IMaterialStreamProvider
{
    public Stream Open(string materialFilePath)
    {
        return engine.Assets.Open(materialFilePath);
    }
}

public class Obj
{
    static readonly private IMaterialStreamProvider _materialStreamProvider = new AssetMaterialStreamProvider();
    static readonly private object _lo = new();
    static readonly private ObjLoaderFactory _objLoaderFactory = new();

    private static ulong _toHash(int vertexIndex, int textureIndex, int normalIndex)
    {
        return
            (((ulong)vertexIndex) & 0x001fffff)
            | ((((ulong)vertexIndex) & 0x001fffff) << 21)
            | ((((ulong)vertexIndex) & 0x001fffff) << 42)
            ;
    }
    
    static public engine.joyce.InstanceDesc LoadModelInstance(string url, out ModelInfo modelInfo)
    {
        InstanceDesc jInstanceDesc = new();
        var objLoader = _objLoaderFactory.Create(_materialStreamProvider);
        var fileStream = engine.Assets.Open(url);
        var loadedObject = objLoader.Load(fileStream);
        uint[] tri = new uint[3];

        modelInfo = new();

        engine.geom.AABB aabb = new();
        Vector3 vCenter = new();
        int nVertices = 0;

        List<ObjLoader.Loader.Data.Material> listMaterials = new();
        foreach (var loadedMaterial in loadedObject.Materials)
        {
            engine.joyce.Material jMaterial = new();
            jMaterial.AlbedoColor =
                ((uint)(loadedMaterial.DiffuseColor.X * 255f))
                | ((uint)(loadedMaterial.DiffuseColor.Y * 255f) << 8)
                | ((uint)(loadedMaterial.DiffuseColor.Z * 255f) << 16)
                | 0xff000000;
            jInstanceDesc.Materials.Add(jMaterial);
            listMaterials.Add(loadedMaterial);
        }

        foreach (var loadedGroup in loadedObject.Groups)
        {
            /*
             * For each of the Wavefront groups we create a mesh
             */
            engine.joyce.Mesh jMesh = engine.joyce.Mesh.CreateListInstance();
            jMesh.Normals = new List<Vector3>();

            /*
             * We map each distinct triplet of indices to an index
             * in our table.
             */
            SortedDictionary<ulong, uint> mapIndices = new();
            foreach (var loadedFace in loadedGroup.Faces)
            {
                for (int idx=0; idx<3; ++idx)
                {
                    int vertexIndex = loadedFace[idx].VertexIndex - 1;
                    int textureIndex = loadedFace[idx].TextureIndex - 1;
                    int normalIndex = loadedFace[idx].NormalIndex - 1;
                    ulong hash = _toHash( vertexIndex, textureIndex, normalIndex);
                    uint myIndex = 0;
                    if (mapIndices.ContainsKey(hash))
                    {
                        myIndex = mapIndices[hash];
                    }
                    else
                    {
                        if (vertexIndex < 0 || vertexIndex >= loadedObject.Vertices.Count)
                        {
                            ErrorThrow($"Vertex index {vertexIndex} out of bounds (> {loadedObject.Vertices.Count}.)",
                                (m) => new InvalidDataException(m));
                        }
                        if (textureIndex < 0 || textureIndex >= loadedObject.Textures.Count)
                        {
                            ErrorThrow($"Texture index {textureIndex} out of bounds (> {loadedObject.Textures.Count}.)",
                                (m) => new InvalidDataException(m));
                        }
                        if (normalIndex < 0 || normalIndex >= loadedObject.Normals.Count)
                        {
                            ErrorThrow($"Vertex index {normalIndex} out of bounds (> {loadedObject.Normals.Count}.)",
                                (m) => new InvalidDataException(m));
                        }

                        Vector3 vertex = new(
                            loadedObject.Vertices[vertexIndex].X,
                            loadedObject.Vertices[vertexIndex].Y,
                            loadedObject.Vertices[vertexIndex].Z
                        );
                        aabb.Add(vertex);
                        vCenter += vertex;
                        nVertices++;
                        
                        jMesh.p(vertex);
                        jMesh.UV(
                            loadedObject.Textures[textureIndex].X,
                            loadedObject.Textures[textureIndex].Y
                            );
                        jMesh.N(
                            loadedObject.Normals[normalIndex].X,
                            loadedObject.Normals[normalIndex].Y,
                            loadedObject.Normals[normalIndex].Z
                            );
                        myIndex = (uint) jMesh.WriteIndexVertices-1;
                        mapIndices[hash] = myIndex;
                    }

                    tri[idx] = myIndex;
                }
                jMesh.Idx(tri[0], tri[1], tri[2]);
            }

            jInstanceDesc.Meshes.Add(jMesh);
            int idxMaterial = listMaterials.IndexOf(loadedGroup.Material);
            jInstanceDesc.MeshMaterials.Add(idxMaterial);
        }

        modelInfo.AABB = aabb;
        modelInfo.Center = vCenter / nVertices;
        return jInstanceDesc;
    }   
}