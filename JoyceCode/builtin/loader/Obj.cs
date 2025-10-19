using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using builtin.tools.Lindenmayer;
using engine;
using ObjLoader.Loader.Loaders;
using engine.joyce;
using static engine.Logger;
using Material = ObjLoader.Loader.Data.Material;

namespace builtin.loader;



class AssetMaterialStreamProvider : IMaterialStreamProvider
{
    public Stream Open(string materialFilePath)
    {
        return engine.Assets.Open(materialFilePath);
    }
}


public class Obj
{
    readonly private IMaterialStreamProvider _materialStreamProvider = new AssetMaterialStreamProvider();
    readonly private object _lo = new();
    readonly private ObjLoaderFactory _objLoaderFactory = new();

    static private uint Vec3ToUint(in ObjLoader.Loader.Data.Vec3 v) => engine.Color.Vector3ToUint(new(v.X, v.Y, v.Z));

    private engine.Engine _engine = I.Get<engine.Engine>();
    
    private static ulong _toHash(int vertexIndex, int textureIndex, int normalIndex)
    {
        return
            (((ulong)vertexIndex) & 0x001fffff)
            | ((((ulong)vertexIndex) & 0x001fffff) << 21)
            | ((((ulong)vertexIndex) & 0x001fffff) << 42)
            ;
    }


    static private bool _isThrustMaterialName(string n)
    {
        return (n.Length >= 6 && n.Substring(0, 6) == "thrust");
    }

    
    static private bool _isStandardLightMaterialName(string n)
    {
        return (n.Length >= 13 && n.Substring(0, 13) == "standardlight");
    }
    

    static private bool _isPrimaryColorMaterialName(string n)
    {
        return (n.Length >= 12 && n.Substring(0, 12) == "primarycolor");
    }


    public void LoadModelInstanceSync(string url,
        ModelProperties modelProperties,
        out Model model)
    {
        var objLoader = _objLoaderFactory.Create(_materialStreamProvider);
        var fileStream = engine.Assets.Open(url);
        var loadedObject = objLoader.Load(fileStream);

        uint[] tri = new uint[3];

        engine.geom.AABB aabb = new();
        Vector3 vCenter = new();
        int nVertices = 0;

        List<engine.joyce.Mesh> meshes = new();
        List<engine.joyce.Material> materials = new();
        List<int> meshMaterials = new();

        List<ObjLoader.Loader.Data.Material> listMaterials = new();

        string primarycolor = "";
        if (modelProperties != null && modelProperties.Properties.TryGetValue("primarycolor", out var col))
        {
            primarycolor = col;
        }

        model = new();
        var mnRoot = model.ModelNodeTree.CreateNode(model);
        foreach (var loadedMaterial in loadedObject.Materials)
        {
            engine.joyce.Material jMaterial = new();
            jMaterial.Name = loadedMaterial.Name;

            /*
             * Handle special materials
             */
            if (_isThrustMaterialName(loadedMaterial.Name))
            {
                /*
                 * Self-lighting engine.
                 */
                jMaterial.EmissiveTexture = I.Get<TextureCatalogue>()
                    .FindColorTexture(Vec3ToUint(loadedMaterial.DiffuseColor));
                jMaterial.Name = "thrust";
            }
            else if (_isStandardLightMaterialName(loadedMaterial.Name))
            {
                /*
                 * Self-lighting engine.
                 */
                jMaterial.EmissiveTexture = I.Get<TextureCatalogue>()
                    .FindColorTexture(Vec3ToUint(loadedMaterial.DiffuseColor));
                jMaterial.Name = "standardlight";
            }
            else if (primarycolor.Length != 0 && _isPrimaryColorMaterialName(loadedMaterial.Name))
            {
                /*
                 * Self-lighting engine.
                 */
                jMaterial.Texture = I.Get<TextureCatalogue>()
                    .FindColorTexture(engine.Color.StringToUInt(primarycolor));
                jMaterial.Name = "primarycolor";
            }
            else
            {
                /*
                 * Standard case
                 */
                jMaterial.Texture = I.Get<TextureCatalogue>()
                    .FindColorTexture(Vec3ToUint(loadedMaterial.DiffuseColor));
                jMaterial.Name = loadedMaterial.Name;
            }

            /*
             * Find the common version of the material instead of creating a new one.
             */
            jMaterial = I.Get<ObjectRegistry<engine.joyce.Material>>().FindLike(jMaterial);
            materials.Add(jMaterial);
            listMaterials.Add(loadedMaterial);
        }

        foreach (var loadedGroup in loadedObject.Groups)
        {
            /*
             * For each of the Wavefront groups we create a mesh
             */
            engine.joyce.Mesh jMesh = engine.joyce.Mesh.CreateListInstance(url);
            jMesh.Normals = new List<Vector3>();

            /*
             * We map each distinct triplet of indices to an index
             * in our table.
             */
            SortedDictionary<ulong, uint> mapIndices = new();
            foreach (var loadedFace in loadedGroup.Faces)
            {
                for (int idx = 0; idx < 3; ++idx)
                {
                    int vertexIndex = loadedFace[idx].VertexIndex - 1;
                    int textureIndex = loadedFace[idx].TextureIndex - 1;
                    int normalIndex = loadedFace[idx].NormalIndex - 1;
                    ulong hash = _toHash(vertexIndex, textureIndex, normalIndex);
                    uint myIndex = 0;
                    if (mapIndices.ContainsKey(hash))
                    {
                        myIndex = mapIndices[hash];
                    }
                    else
                    {
                        if (vertexIndex < 0 || vertexIndex >= loadedObject.Vertices.Count)
                        {
                            ErrorThrow(
                                $"Vertex index {vertexIndex} out of bounds (> {loadedObject.Vertices.Count}.)",
                                (m) => new InvalidDataException(m));
                        }

                        if (textureIndex < 0 || textureIndex >= loadedObject.Textures.Count)
                        {
                            ErrorThrow(
                                $"Texture index {textureIndex} out of bounds (> {loadedObject.Textures.Count}.)",
                                (m) => new InvalidDataException(m));
                        }

                        if (normalIndex < 0 || normalIndex >= loadedObject.Normals.Count)
                        {
                            ErrorThrow(
                                $"Vertex index {normalIndex} out of bounds (> {loadedObject.Normals.Count}.)",
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
                        jMesh.UVUnsafe(
                            loadedObject.Textures[textureIndex].X,
                            loadedObject.Textures[textureIndex].Y
                        );
                        jMesh.N(
                            loadedObject.Normals[normalIndex].X,
                            loadedObject.Normals[normalIndex].Y,
                            loadedObject.Normals[normalIndex].Z
                        );
                        myIndex = (uint)jMesh.WriteIndexVertices - 1;
                        mapIndices[hash] = myIndex;
                    }

                    tri[idx] = myIndex;
                }

                jMesh.Idx(tri[0], tri[1], tri[2]);
            }

            meshes.Add(jMesh);
            int idxMaterial = listMaterials.IndexOf(loadedGroup.Material);
            meshMaterials.Add(idxMaterial);
        }
        
        InstanceDesc id = new(meshes, meshMaterials, materials, new List<ModelNode>() {mnRoot}, 100f);
        mnRoot.SetInstanceDesc(id);
        mnRoot.SetModel(model);
        model.ModelNodeTree.RootNode = mnRoot;
            
        model.Polish(null);
        // TXWTODO: read the maximal distance from some properties
    }


    public Task<Model> LoadModelInstance(string url, ModelProperties modelProperties)
    {
        return _engine.Run(() =>
        {
            LoadModelInstanceSync(url, modelProperties, out var model);
            return model;
        });
    }
}