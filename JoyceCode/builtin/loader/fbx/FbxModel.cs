using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using engine;
using engine.joyce;
using engine.joyce.components;
using Silk.NET.Assimp;
using AssimpMesh = Silk.NET.Assimp.Mesh;
using Material = Silk.NET.Assimp.Material;
using static engine.Logger;

namespace builtin.loader.fbx;


public class FbxModel : IDisposable
{
    static  private Assimp _assimp;
    private List<Texture> _texturesLoaded = new List<Texture>();
    public string Directory { get; protected set; } = string.Empty;

    private static void _needAssimp()
    {
        if (null == _assimp)
        {
            _assimp = Assimp.GetApi();
            //var customAssimpLibraryNameContainer = new CustomAssimpLibraryNameContainer();
            //_assimp = new(Silk.NET.Assimp.Assimp.CreateDefaultContext(customAssimpLibraryNameContainer.GetLibraryNames()));
        }
    }


    private unsafe string GetMetadata(Scene* scene, string key, string defaultValue="")
    {
        if (null == scene || null == scene->MMetaData)
        {
            return defaultValue;
        }

        Metadata* metadata = scene->MMetaData;
        for (uint i = 0; i < metadata->MNumProperties; i++)
        {
            if (key == metadata->MKeys[i].ToString())
            {
                void* p = metadata->MValues[i].MData;
                switch (metadata->MValues[i].MType)
                {
                    case MetadataType.Bool:
                        return (*(bool*)p).ToString();
                    
                    case MetadataType.Int32:
                        return (*(int*)p).ToString();

                    case MetadataType.Uint64:
                        return (*(ulong*)p).ToString();

                    case MetadataType.Float:
                        return (*(float*)p).ToString();

                    case MetadataType.Double:
                        return (*(double*)p).ToString();
                    
                    case MetadataType.Aistring:
                    case MetadataType.Aivector3D: 
                    case MetadataType.Aimetadata:
                        return defaultValue;
                    
                    case MetadataType.Int64:
                        return (*(long*)p).ToString();
                    case MetadataType.Uint32:
                        return (*(uint*)p).ToString();
                }
            }
        }
        return defaultValue;
    }

    
    unsafe public void Load(string path, out engine.joyce.Model model)
    {
        model = new engine.joyce.Model();

        Directory = path;
        _needAssimp();

        FileIO fileIO = fbx.Assets.Get();
        Scene* pScene = null;
        FileIO* pFileIO = &fileIO;
        pScene = _assimp.ImportFileEx(
            path,
            (uint)PostProcessSteps.Triangulate,
            pFileIO
        );
        if (pScene == null || pScene->MFlags == Assimp.SceneFlagsIncomplete || pScene->MRootNode == null)
        {
            var error = _assimp.GetErrorStringS();
            throw new Exception(error);
        }

        model.RootNode = ProcessNode(pScene->MRootNode, pScene);
        
        var strUnitscale = GetMetadata(pScene, "UnitScaleFactor", "1.");
        float unitscale = float.Parse(strUnitscale, CultureInfo.InvariantCulture);
        model.RootNode.Transform.Matrix = Matrix4x4.CreateScale((unitscale)/100f) * model.RootNode.Transform.Matrix;

        // TXWTODO: How to free scene?
    }
    
    
    private unsafe engine.joyce.ModelNode ProcessNode(Node* node, Scene* scene)
    {
        engine.joyce.ModelNode mn = new();

        /*
         * If there are meshes, add them.
         */
        if (node->MNumMeshes > 0)
        {
            engine.joyce.MatMesh matMesh = new();
            for (var i = 0; i < node->MNumMeshes; i++)
            {
                var pMesh = scene->MMeshes[node->MMeshes[i]];
                if (pMesh != null)
                {
                    var fbxMesh = ProcessMesh(pMesh, scene);
                    fbxMesh.AddToMatmesh(matMesh);
                }
            }

            mn.InstanceDesc = InstanceDesc.CreateFromMatMesh(matMesh, 400f);
        }

        /*
         * If there are children, add them.
         */
        if (node->MNumChildren > 0)
        {
            Trace($"{node->MNumChildren} chilrren.");
            mn.Children = new List<ModelNode>();
            for (var i = 0; i < node->MNumChildren; i++)
            {
                mn.Children.Add(ProcessNode(node->MChildren[i], scene));
            }
        }

        var mToParent = Matrix4x4.Transpose(node->MTransformation);
        
        mn.Transform = new Transform3ToParent(
            true, 0xffffffff, mToParent);
        // Trace($"My transform is {mn.Transform.Matrix}");
        
        return mn;
    }
    

    private unsafe Mesh ProcessMesh(AssimpMesh* mesh, Scene* scene)
    {
        // data to fill
        List<Vertex> vertices = new List<Vertex>();
        List<uint> indices = new List<uint>();
        List<Texture> textures = new List<Texture>();

        // walk through each of the mesh's vertices
        for (uint i = 0; i < mesh->MNumVertices; i++)
        {
            Vertex vertex = new Vertex();
            vertex.BoneIds = new int[Vertex.MAX_BONE_INFLUENCE];
            vertex.Weights = new float[Vertex.MAX_BONE_INFLUENCE];

            vertex.Position = mesh->MVertices[i];

            // normals
            if (mesh->MNormals != null)
                vertex.Normal = mesh->MNormals[i];
            // tangent
            if (mesh->MTangents != null)
                vertex.Tangent = mesh->MTangents[i];
            // bitangent
            if (mesh->MBitangents != null)
                vertex.Bitangent = mesh->MBitangents[i];

            // texture coordinates
            if (mesh->MTextureCoords[0] != null) // does the mesh contain texture coordinates?
            {
                // a vertex can contain up to 8 different texture coordinates. We thus make the assumption that we won't 
                // use models where a vertex can have multiple texture coordinates so we always take the first set (0).
                Vector3 texcoord3 = mesh->MTextureCoords[0][i];
                vertex.TexCoords = new Vector2(texcoord3.X, texcoord3.Y);
            }

            vertices.Add(vertex);
        }

        // now wak through each of the mesh's faces (a face is a mesh its triangle) and retrieve the corresponding vertex indices.
        for (uint i = 0; i < mesh->MNumFaces; i++)
        {
            Face face = mesh->MFaces[i];
            // retrieve all indices of the face and store them in the indices vector
            for (uint j = 0; j < face.MNumIndices; j++)
                indices.Add(face.MIndices[j]);
        }

        // process materials
        Material* material = scene->MMaterials[mesh->MMaterialIndex];
        // we assume a convention for sampler names in the shaders. Each diffuse texture should be named
        // as 'texture_diffuseN' where N is a sequential number ranging from 1 to MAX_SAMPLER_NUMBER. 
        // Same applies to other texture as the following list summarizes:
        // diffuse: texture_diffuseN
        // specular: texture_specularN
        // normal: texture_normalN

        // 1. diffuse maps
        var diffuseMaps = LoadMaterialTextures(material, TextureType.Diffuse, "texture_diffuse");
        if (diffuseMaps.Any())
            textures.AddRange(diffuseMaps);
        // 2. specular maps
        var specularMaps = LoadMaterialTextures(material, TextureType.Specular, "texture_specular");
        if (specularMaps.Any())
            textures.AddRange(specularMaps);
        // 3. normal maps
        var normalMaps = LoadMaterialTextures(material, TextureType.Height, "texture_normal");
        if (normalMaps.Any())
            textures.AddRange(normalMaps);
        // 4. height maps
        var heightMaps = LoadMaterialTextures(material, TextureType.Ambient, "texture_height");
        if (heightMaps.Any())
            textures.AddRange(heightMaps);

        // return a mesh object created from the extracted mesh data
        var result = new Mesh(BuildVertices(vertices), BuildIndices(indices), textures);
        return result;
    }

    private unsafe List<Texture> LoadMaterialTextures(Material* mat, TextureType type, string typeName)
    {
        var textureCount = _assimp.GetMaterialTextureCount(mat, type);
        List<Texture> textures = new List<Texture>();
        for (uint i = 0; i < textureCount; i++)
        {
            AssimpString path;
            _assimp.GetMaterialTexture(mat, type, i, &path, null, null, null, null, null, null);
            bool skip = false;
            for (int j = 0; j < _texturesLoaded.Count; j++)
            {
                if (_texturesLoaded[j].Path == path)
                {
                    textures.Add(_texturesLoaded[j]);
                    skip = true;
                    break;
                }
            }

            if (!skip)
            {
                var texture = new Texture(Directory, type);
                texture.Path = path;
                textures.Add(texture);
                _texturesLoaded.Add(texture);
            }
        }

        return textures;
    }

    private float[] BuildVertices(List<Vertex> vertexCollection)
    {
        var vertices = new List<float>();

        foreach (var vertex in vertexCollection)
        {
            vertices.Add(vertex.Position.X);
            vertices.Add(vertex.Position.Y);
            vertices.Add(vertex.Position.Z);
            vertices.Add(vertex.TexCoords.X);
            vertices.Add(vertex.TexCoords.Y);
        }

        return vertices.ToArray();
    }

    private uint[] BuildIndices(List<uint> indices)
    {
        return indices.ToArray();
    }

    public void Dispose()
    {
        _texturesLoaded = null;
    }

}