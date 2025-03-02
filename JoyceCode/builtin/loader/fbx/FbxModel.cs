using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using engine;
using engine.joyce;
using engine.joyce.components;
using Silk.NET.Assimp;
using AssimpMesh = Silk.NET.Assimp.Mesh;
using Material = Silk.NET.Assimp.Material;
using static engine.Logger;
using static builtin.extensions.Vector4Extensions;

namespace builtin.loader.fbx;


public class FbxModel : IDisposable
{
    static  private Assimp _assimp;
    private List<Texture> _texturesLoaded = new List<Texture>();
    public string Directory { get; protected set; } = string.Empty;
    private Model? _model = null;
    private unsafe Scene* _scene = null;
    

    private static void _needAssimp()
    {
        if (null == _assimp)
        {
            _assimp = Assimp.GetApi();
            //var customAssimpLibraryNameContainer = new CustomAssimpLibraryNameContainer();
            //_assimp = new(Silk.NET.Assimp.Assimp.CreateDefaultContext(customAssimpLibraryNameContainer.GetLibraryNames()));
        }
    }


    private unsafe string GetMetadata(string key, string defaultValue="")
    {
        if (null == _scene->MMetaData)
        {
            return defaultValue;
        }

        Metadata* metadata = _scene->MMetaData;
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
        if (null != _model)
        {
            ErrorThrow<InvalidOperationException>($"Unable to load model {path}. model already loaded.");
        }
        _model = model = new engine.joyce.Model();

        Directory = path;
        _needAssimp();

        FileIO fileIO = fbx.Assets.Get();
        FileIO* pFileIO = &fileIO;
        _scene = _assimp.ImportFileEx(
            path,
            (uint)PostProcessSteps.Triangulate,
            pFileIO
        );
        if (_scene == null || _scene->MFlags == Assimp.SceneFlagsIncomplete || _scene->MRootNode == null)
        {
            var error = _assimp.GetErrorStringS();
            throw new Exception(error);
        }

        model.RootNode = ProcessNode(_scene->MRootNode);
        
        var strUnitscale = GetMetadata("UnitScaleFactor", "1.");
        float unitscale = float.Parse(strUnitscale, CultureInfo.InvariantCulture);
        model.RootNode.Transform.Matrix = Matrix4x4.CreateScale((unitscale)/100f) * model.RootNode.Transform.Matrix;

        // TXWTODO: How to free scene?
    }


    private unsafe engine.joyce.Material FindMaterial(uint materialIndex, AssimpMesh.MColorsBuffer* colorsBuffer)
    {
        /*
         * We create a new material looking it up in our internal cache
         */
        if (materialIndex >= _scene->MNumMaterials)
        {
            Error($"Material index {materialIndex} is out of range.");
            return new();
        }
        
        // process materials
        Material* aiMat = _scene->MMaterials[materialIndex];
        // we assume a convention for sampler names in the shaders. Each diffuse texture should be named
        // as 'texture_diffuseN' where N is a sequential number ranging from 1 to MAX_SAMPLER_NUMBER. 
        // Same applies to other texture as the following list summarizes:
        // diffuse: texture_diffuseN
        // specular: texture_specularN
        // normal: texture_normalN

        // 1. diffuse maps
        var diffuseMaps = LoadMaterialTextures(aiMat, TextureType.Diffuse, "texture_diffuse");
        
        /*if (diffuseMaps.Any())
            textures.AddRange(diffuseMaps);*/
        // 2. specular maps
        var specularMaps = LoadMaterialTextures(aiMat, TextureType.Specular, "texture_specular");
        /*if (specularMaps.Any())
            textures.AddRange(specularMaps);*/
        // 3. normal maps
        var normalMaps = LoadMaterialTextures(aiMat, TextureType.Height, "texture_normal");
        /*if (normalMaps.Any())
            textures.AddRange(normalMaps);*/
        // 4. height maps
        var heightMaps = LoadMaterialTextures(aiMat, TextureType.Ambient, "texture_height");
        /*if (heightMaps.Any())
            textures.AddRange(heightMaps);*/

        // new() { Texture = I.Get<TextureCatalogue>().FindColorTexture(0xff888888) };
        engine.joyce.Material jMaterial = new();
        if (diffuseMaps.Any())
        {
            var path = diffuseMaps[0].Path;
            engine.joyce.Texture? jTexture = null;
            
            /*
             * First try to find the texture in the atlas, with and without extension
             * ... only then load it without atlas.
             */
            if (
                !I.Get<TextureCatalogue>().TryGetTexture(
                    Path.GetFileNameWithoutExtension(path), null, out jTexture)
                && 
                !I.Get<TextureCatalogue>().TryGetTexture(
                    path, null, out jTexture)
                &&
                !I.Get<TextureCatalogue>().TryGetTexture(
                    diffuseMaps[0].Path, null, out jTexture))
            {
                jTexture = I.Get<TextureCatalogue>().FindColorTexture(0xff888888);
            }

            jMaterial.Texture = jTexture;
        }

        if (colorsBuffer != null)
        {
            if (colorsBuffer->Element0 != null)
            {
                Vector4 c4Albedo = *(colorsBuffer->Element0);
                jMaterial.AlbedoColor = c4Albedo.ToRGBA32();
            }
        }

        return jMaterial;
    }
    
    
    private unsafe engine.joyce.ModelNode ProcessNode(Node* node)
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
                var pMesh = _scene->MMeshes[node->MMeshes[i]];
                if (pMesh != null)
                {
                    var jMesh = ProcessMesh(pMesh);
                    
                    /*
                     * Now find the material associated with the mesh
                     */
                    var jMaterial = FindMaterial(pMesh->MMaterialIndex, &pMesh->MColors);
                    
                    matMesh.Add(jMaterial, jMesh);

                }
            }

            mn.InstanceDesc = InstanceDesc.CreateFromMatMesh(matMesh, 400f);
        }

        /*
         * If there are children, add them.
         */
        if (node->MNumChildren > 0)
        {
            Trace($"{node->MNumChildren} children.");
            mn.Children = new List<ModelNode>();
            for (var i = 0; i < node->MNumChildren; i++)
            {
                mn.Children.Add(ProcessNode(node->MChildren[i]));
            }
        }

        var mToParent = Matrix4x4.Transpose(node->MTransformation);
        
        mn.Transform = new Transform3ToParent(
            true, 0xffffffff, mToParent);
        // Trace($"My transform is {mn.Transform.Matrix}");
        
        return mn;
    }

    
    private unsafe engine.joyce.Mesh ProcessMesh(AssimpMesh* mesh)
    {
        // data to fill
        List<Vertex> vertices = new List<Vertex>();
        List<uint> indices = new List<uint>();
        
        uint nMeshVertices = mesh->MNumVertices;

        // walk through each of the mesh's vertices
        for (uint i = 0; i < nMeshVertices; i++)
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

        
        /*
         * return a mesh object created from the extracted mesh data
         */
        var fbxMesh = new Mesh(BuildVertices(vertices), BuildIndices(indices) /* , textures */);
        var jMesh = fbxMesh.ToJoyceMesh();
        
       
        /*
         * If there is a bone, create it.
         */
        if (mesh->MNumBones > 0 && mesh->MBones != null)
        {
            BoneMesh[] boneMeshes = new BoneMesh [mesh->MNumBones];
            var skeleton = _model.FindSkeleton(); 
            
            for (int i = 0; i < mesh->MNumBones; ++i)
            {
                var aiBone = mesh->MBones[i];

                var jBone = skeleton.FindBone(aiBone->MName.ToString());
                jBone.InverseMatrix = Matrix4x4.Transpose(aiBone->MOffsetMatrix);
                var nBoneVertices = aiBone->MNumWeights;
                boneMeshes[i] = new BoneMesh(jBone, nBoneVertices);
            
                for (int j = 0; j < nBoneVertices; ++j)
                {
                    boneMeshes[i].SetVertexWeight(aiBone->MWeights[j].MVertexId, aiBone->MWeights[j].MWeight);
                }
            }
            
            /*
             * Now, if this is more than the maximum of bones per mesh,
             * take only the four most important.
             */
            
            // TXWTODO: Write this
            
            /*
             * Now write the actual weights and bone indices to the actual mesh data structure.
             */
            uint nBones = UInt32.Min(4, mesh->MNumBones);
            jMesh.BoneIndices = new List<Byte4>((int)nMeshVertices);
            jMesh.BoneWeights = new List<Vector4>((int)nMeshVertices);
            for (int j = 0; j < nBones; j++)
            {
                for (int k = 0; k < nMeshVertices; ++k)
                {
                    var b4 = jMesh.BoneIndices[k];
                    b4[j] = (byte)boneMeshes[j].Bone.Index;
                    jMesh.BoneIndices[k] = b4;
                    var w4 = jMesh.BoneWeights[k];
                    w4[j] = boneMeshes[j].VertexWeights[k].Weight;
                    jMesh.BoneWeights[k] = w4;
                }
            }
        }
        
        return jMesh;
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