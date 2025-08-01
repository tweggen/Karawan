using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using builtin.extensions;
using engine;
using engine.joyce;
using engine.joyce.components;
using Silk.NET.Assimp;
using AssimpMesh = Silk.NET.Assimp.Mesh;
using Material = Silk.NET.Assimp.Material;
using static engine.Logger;
using static builtin.extensions.Vector4Extensions;
using VertexWeight = engine.joyce.VertexWeight;

namespace builtin.loader.fbx;

public class FbxModel : IDisposable
{
    static  private Assimp _assimp;
    private List<Texture> _texturesLoaded = new List<Texture>();
    public string Directory { get; protected set; } = string.Empty;
    private Model? _model = null;
    private unsafe Scene* _scene = null;

    private static object _slo = new();

    private static void _needAssimp()
    {
        lock (_slo)
        {
            try
            {

                if (null == _assimp)
                {
                    System.Console.WriteLine("Loading assimp...");
                    _assimp = Assimp.GetApi();
                    //var customAssimpLibraryNameContainer = new CustomAssimpLibraryNameContainer();
                    //_assimp = new(Silk.NET.Assimp.Assimp.CreateDefaultContext(customAssimpLibraryNameContainer.GetLibraryNames()));
                }
                else
                {
                    System.Console.WriteLine("Assimp previously had been loaded...");
                }
            }
            catch (Exception e)
            {
                System.Console.WriteLine("Exception instantiating assimp: "+e);
            }

        }
    }


    private static unsafe void DumpMetadata(Scene* pScene)
    {
        if (null == pScene->MMetaData)
        {
            return;
        }

        Metadata* metadata = pScene->MMetaData;
        for (uint i = 0; i < metadata->MNumProperties; i++)
        {
            string strValue = "(unknown)";
            void* p = metadata->MValues[i].MData;
            switch (metadata->MValues[i].MType)
            {
                case MetadataType.Bool:
                    strValue = (*(bool*)p).ToString();
                    break;

                case MetadataType.Int32:
                    strValue = (*(int*)p).ToString();
                    break;

                case MetadataType.Uint64:
                    strValue = (*(ulong*)p).ToString();
                    break;

                case MetadataType.Float:
                    strValue = (*(float*)p).ToString();
                    break;

                case MetadataType.Double:
                    strValue = (*(double*)p).ToString();
                    break;

                case MetadataType.Aistring:
                case MetadataType.Aivector3D:
                case MetadataType.Aimetadata:
                    break;

                case MetadataType.Int64:
                    strValue = (*(long*)p).ToString();
                    break;

                case MetadataType.Uint32:
                    strValue = (*(uint*)p).ToString();
                    break;

            }

            Trace($"\"{metadata->MKeys[i].ToString()}\": \"{strValue}\"");
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

    
    private unsafe void _compareBoneHierarchies(Scene* meshScene, Scene* animScene) {
        // Check if root nodes match
        _compareNodeRecursive(meshScene->MRootNode, animScene->MRootNode);
    }

    private unsafe void _compareNodeRecursive(Node* meshNode, Node* animNode) {
        if (null == meshNode || null == animNode) {
            ErrorThrow<InvalidOperationException>("Hierarchy mismatch: one node is null\n");
        }
    
        if (meshNode->MName != animNode->MName) {
            ErrorThrow<InvalidOperationException>($"Node name mismatch: {meshNode->MName} vs {animNode->MName} vs %s\n");
        }
    
        if (meshNode->MNumChildren != animNode->MNumChildren) {
            ErrorThrow<InvalidOperationException>($"Child count mismatch for {meshNode->MName}: {meshNode->MNumChildren} vs {animNode->MNumChildren}\n");
        }
    
        // Compare transformations
        if (!EqualsRoughly(meshNode->MTransformation, animNode->MTransformation)) {
            ErrorThrow<InvalidOperationException>($"Transformation differs for node: {meshNode->MName}\n");
        }
    }
    
    
    /**
     * Load a given fbx file into this model.
     * You can also pass additional files to add e.g. animation data.
     */
    public unsafe void Load(string path, List<string>? additionalUrls, float scale, out engine.joyce.Model model)
    {
        if (null != _model)
        {
            ErrorThrow<InvalidOperationException>($"Unable to load model {path}. model already loaded.");
        }

        /*
         * Prepare loading modes
         */
        
        /*
         * That's a bit hacky, but load the main file animations and the bones only, if
         * there are no additional files.
         */
        bool haveAdditionalFiles = (additionalUrls == null || additionalUrls.Count == 0);
        bool loadMainAnimations = haveAdditionalFiles;
        bool loadMainNodes = haveAdditionalFiles;

        /*
         * Prepare data structures.
         */
        _model = model = new engine.joyce.Model();
        _model.Name = path;
        _model.MapAnimations = new();

        /*
         * Load the actual file.
         */
        
        Directory = path;
        _needAssimp();
        
        FileIO fileIO = fbx.Assets.Get();
        FileIO* pFileIO = &fileIO;
        _scene = _assimp.ImportFileEx(
            path,
            (uint)PostProcessSteps.Triangulate,
            pFileIO
        );
        Trace($"Loaded \"{path}\"");
        DumpMetadata(_scene);
        if (_scene == null || _scene->MFlags == Assimp.SceneFlagsIncomplete || _scene->MRootNode == null)
        {
            var error = _assimp.GetErrorStringS();
            throw new Exception(error);
        }

        model.RootNode = _processNode(null, _scene->MRootNode, true, loadMainNodes, out var _);
        _model.MapNodes[model.RootNode.Name] = model.RootNode;
        
        /*
         * Now load all the animations. First the ones from the main file.
         */

        if (loadMainAnimations)
        {
            _loadAnimations(_scene, "");
        }

        /*
         * Now go through the extra fbx files and load the animations from
         * them to this model.
         */
        if (additionalUrls != null)
        {
            foreach (var url in additionalUrls)
            {
                try
                {
                    Trace($"Import additional animation data from {url}...");
                    var additionalScene = _assimp.ImportFileEx(
                        url,
                        (uint)PostProcessSteps.Triangulate,
                        pFileIO
                    );
                    if (additionalScene == null  || additionalScene->MRootNode == null)
                    {
                        continue;
                    }

                    DumpMetadata(additionalScene);

                    _compareBoneHierarchies(additionalScene, _scene);
                    
                    /*
                     * We parse the additional files' children to make sure they match.
                     */
                    _processNode(null, additionalScene->MRootNode, false, true, out var _);
                    
                    string strFallbackName = url;
                    int idx = strFallbackName.LastIndexOf('/');
                    if (-1 != idx)
                    {
                        if (idx + 1 < strFallbackName.Length)
                        {
                            strFallbackName = strFallbackName.Substring(idx + 1);
                        }
                    }
                    idx = strFallbackName.LastIndexOf('.');
                    if (0 < idx)
                    {
                        strFallbackName = strFallbackName.Substring(0, idx);
                    }
                    _loadAnimations(additionalScene, strFallbackName);
                    Trace($"Done importing additional animation data from {url}.");
                }
                catch (Exception e)
                {
                    Error($"Exception while loading additional animation data: {e}");
                }
            }
        }
        
        var strUnitscale = GetMetadata("UnitScaleFactor", "1.");
        float unitscale = float.Parse(strUnitscale, CultureInfo.InvariantCulture);
        model.Scale = unitscale / 100f * scale;

        if (GetMetadata("CustomFrameRate", "-1") == "24")
        {
            model.WorkAroundInverseRestPose = true;
        }
        else
        {
            model.WorkAroundInverseRestPose = false;
        }

        /*
         * Baking animations must include the root matrix corrections.
         */
        model.BakeAnimations();

        model.RootNode.Transform.Matrix = Matrix4x4.CreateScale(model.Scale) * model.RootNode.Transform.Matrix;

        model.Polish();
    }
    
    
    private unsafe void _loadAnimations(Scene *scene, string strFallbackName)
    {
        if (null == scene->MAnimations)
        {
            return;
        }
        
        uint nAnimations = scene->MNumAnimations;
        if (0 == nAnimations)
        {
            return;
        }

        for (int i = 0; i < nAnimations; ++i)
        {
            var aiAnim = scene->MAnimations[i];
            if (null == aiAnim || 0 == aiAnim->MNumChannels)
            {
                continue;
            }

            uint nChannels = aiAnim->MNumChannels;

            ModelAnimation ma = _model.CreateAnimation();
            ma.Name = String.IsNullOrWhiteSpace(strFallbackName)?aiAnim->MName.ToString():strFallbackName;
            Trace($"Found Animation \"{ma.Name}\" with {nChannels} channels.");
            ma.Duration = (float)aiAnim->MDuration / (float)aiAnim->MTicksPerSecond;
            ma.TicksPerSecond = (float)aiAnim->MTicksPerSecond;
            ma.NTicks = (aiAnim->MTicksPerSecond < 0.015f) ? 1 : (uint)(aiAnim->MDuration / aiAnim->MTicksPerSecond);
            ma.MapChannels = new();
            uint nFrames = UInt32.Max((uint)(ma.Duration * 60f), 1);
            ma.NFrames = nFrames;
            _model.PushAnimFrames(nFrames);

            for (int j = 0; j < nChannels; ++j)
            {
                var aiChannel = aiAnim->MChannels[j];
                if (null == aiChannel)
                {
                    continue;
                }
                
                string channelNodeName = aiChannel->MNodeName.ToString();
                // Trace($"Animation \"{ma.Name}\" controls channel: {channelNodeName}");
                if (!_model.MapNodes.ContainsKey(channelNodeName))
                {
                    Warning($"Found animation channel for unknown node {channelNodeName}, ignoring.");
                    continue;
                }
                ModelNode channelNode = _model.MapNodes[channelNodeName];

                if (ma.MapChannels.ContainsKey(channelNode))
                {
                    Warning($"Found duplicate animation channel for {channelNodeName}, ignoring.");
                    continue;
                }

                /*
                 * Check, if we already have a bone data structure for this bone
                 */
                uint nPositionKeys = aiChannel->MNumPositionKeys;
                uint nScalingKeys = aiChannel->MNumScalingKeys;
                uint nRotationKeys = aiChannel->MNumRotationKeys;

                if (0 == nPositionKeys && 0 == nScalingKeys && 0 == nRotationKeys)
                {
                    continue;
                }

                ModelAnimChannel mac = ma.CreateChannel(channelNode,
                    (nPositionKeys != 0) ? new KeyFrame<Vector3>[nPositionKeys] : null,
                    (nRotationKeys != 0) ? new KeyFrame<Quaternion>[nRotationKeys] : null,
                    (nScalingKeys != 0) ? new KeyFrame<Vector3>[nScalingKeys] : null
                );

                for (int l = 0; l < nPositionKeys; ++l)
                {
                    mac.Positions![l] = new()
                    {
                        Time = (float)aiChannel->MPositionKeys[l].MTime / ma.TicksPerSecond,
                        Value = aiChannel->MPositionKeys[l].MValue
                    };
                }
                
                for (int l = 0; l < nScalingKeys; ++l)
                {
                    mac.Scalings![l] = new()
                    {
                        Time = (float)aiChannel->MScalingKeys[l].MTime / ma.TicksPerSecond,
                        Value = aiChannel->MScalingKeys[l].MValue
                    };
                }
                
                for (int l = 0; l < nRotationKeys; ++l)
                {
                    mac.Rotations![l] = new()
                    {
                        Time = (float)aiChannel->MRotationKeys[l].MTime / ma.TicksPerSecond,
                        Value = aiChannel->MRotationKeys[l].MValue
                    };
                }
                
                ma.MapChannels[channelNode] = mac;
                mac.Target = channelNode;
            }

            _model.MapAnimations[ma.Name] = ma;
        }
    }


    private unsafe engine.joyce.Material _findMaterial(uint materialIndex, AssimpMesh.MColorsBuffer* colorsBuffer)
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
            jMaterial.Name = path;
        }

        if (colorsBuffer != null)
        {
            if (colorsBuffer->Element0 != null)
            {
                Vector4 c4Albedo = *(colorsBuffer->Element0);
                /*
                * It would be correct to consider the albedocolor,
                * however, our lighting model would add the color
                * rather than multiplying.
                */
//                 jMaterial.AlbedoColor = c4Albedo.ToRGBA32();
                // jMaterial.AlbedoColor = 0xffffffff;
            }
        }

        return I.Get<ObjectRegistry<engine.joyce.Material>>().FindLike(jMaterial);
    }
    
    
    private unsafe engine.joyce.ModelNode _processNode(
        ModelNode mnParent,
        Node* node, bool loadMeshes, 
        bool loadMainNodes,
        out bool meshInOrBelowMe)
    {
        string strName = node->MName.ToString();
        meshInOrBelowMe = false;
        var skeleton = _model!.FindSkeleton(); 

        /*
         * We may be asked to load contents for a node that previously had been loaded.
         * Our job is to enrich the node, not to override it.
         * To have some security while debugging, we verify that the data does
         * not contradict.
         */
        bool isOverridingNode = false;
        bool iHaveMesh = false;
        ModelNode mn = null; 
        if (_model!.MapNodes.ContainsKey(strName))
        {
            mn = _model.MapNodes[strName];
            
            /*
             * We silently assume the similarily named node also had the same parent.
             */
            isOverridingNode = true;
        }
        else
        {
            mn = _model.CreateNode();
            mn.Name = strName;
        }
        
        /*
         * If there are meshes, add them.
         * We do not support adding meshes. 
         */
        if (loadMeshes && node->MNumMeshes > 0)
        {
            if (isOverridingNode)
            {
                ErrorThrow<InvalidOperationException>($"Node {mn.Name} already contained meshes. Mesh extension not supported.");
            }
            
            engine.joyce.MatMesh matMesh = new();
            for (var i = 0; i < node->MNumMeshes; i++)
            {
                var pMesh = _scene->MMeshes[node->MMeshes[i]];
                if (pMesh != null)
                {
                    var jMesh = _processMesh(pMesh);
                    
                    /*
                     * Now find the material associated with the mesh
                     */
                    var jMaterial = _findMaterial(pMesh->MMaterialIndex, &pMesh->MColors);
                    
                    matMesh.Add(jMaterial, jMesh, mn);

                }
            }

            mn.InstanceDesc = InstanceDesc.CreateFromMatMesh(matMesh, 400f);
            meshInOrBelowMe = true;
        }

        /*
         * If there are children, parsing them according to load mode.
         */
        if (node->MNumChildren > 0)
        {
            for (var i = 0; i < node->MNumChildren; i++)
            {
                var aiChild = node->MChildren[i];
                var mnChild = _processNode(mn, aiChild, loadMeshes, loadMainNodes, out var childOrBelowHasMesh);
                meshInOrBelowMe |= childOrBelowHasMesh;
                bool isChildNewNode = !_model.MapNodes.ContainsKey(mnChild.Name);
                
                /*
                 * If this is a new node,
                 * we want to add this node if it either contains a child with a
                 * mesh or we are in loadMainNodes mode.
                 */
                if (isChildNewNode &&
                    (childOrBelowHasMesh || loadMainNodes)
                   )
                {
                    mn.AddChild(mnChild);
                    _model!.MapNodes[mnChild.Name] = mnChild;
                    
                    /*
                     * Also look, if this node is known in the skeleton.
                     * If not, add it.
                     */
                    if (!skeleton.MapBones.ContainsKey(mnChild.Name))
                    {
                        skeleton.FindBone(mnChild.Name);
                    }
                }
            }
        }

        if (true || loadMainNodes)
        {
            var mToParent = Matrix4x4.Transpose(node->MTransformation);

            if (isOverridingNode)
            {
                if (!EqualsRoughly(mn.Transform.Matrix, mToParent))
                {
                    Warning($"Model already contained a node with the same name \"{strName}\".");
                    Warning(
                        $"Loading additional fbx node \"{mn.Name}\" matrix mismatch: \nhad {mn.Transform.Matrix}, \nnow {mToParent}.");
                }
                // mn.Transform = new Transform3ToParent(true, 0xffffffff, mToParent);
            }
            else
            {
                mn.Transform = new Transform3ToParent(true, 0xffffffff, mToParent);
            }

        }
        else
        {
            mn.Transform = new Transform3ToParent(true, 0xffffffff, Matrix4x4.Identity);
        }

        return mn;
    }


    private static bool EqualsRoughly(in Matrix4x4 a, in Matrix4x4 b)
    {
        float scale = 1000f;
        float bias = 500f;
        
        if (false
            || ((int)(a.M11 * scale+bias)) != ((int)(b.M11 * scale+bias))
            || ((int)(a.M12 * scale+bias)) != ((int)(b.M12 * scale+bias))
            || ((int)(a.M13 * scale+bias)) != ((int)(b.M13 * scale+bias))
            || ((int)(a.M14 * scale+bias)) != ((int)(b.M14 * scale+bias))
            || ((int)(a.M21 * scale+bias)) != ((int)(b.M21 * scale+bias))
            || ((int)(a.M22 * scale+bias)) != ((int)(b.M22 * scale+bias))
            || ((int)(a.M23 * scale+bias)) != ((int)(b.M23 * scale+bias))
            || ((int)(a.M24 * scale+bias)) != ((int)(b.M24 * scale+bias))
            || ((int)(a.M31 * scale+bias)) != ((int)(b.M31 * scale+bias))
            || ((int)(a.M32 * scale+bias)) != ((int)(b.M32 * scale+bias))
            || ((int)(a.M33 * scale+bias)) != ((int)(b.M33 * scale+bias))
            || ((int)(a.M34 * scale+bias)) != ((int)(b.M34 * scale+bias))
            || ((int)(a.M41 * scale+bias)) != ((int)(b.M41 * scale+bias))
            || ((int)(a.M42 * scale+bias)) != ((int)(b.M42 * scale+bias))
            || ((int)(a.M43 * scale+bias)) != ((int)(b.M43 * scale+bias))
            || ((int)(a.M44 * scale+bias)) != ((int)(b.M44 * scale+bias))
           )
        {
            return false;
        }

        return true;
    }
    
    
    private unsafe engine.joyce.Mesh _processMesh(AssimpMesh* mesh)
    {
        try
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
                {
                    vertex.Normal = mesh->MNormals[i];
                }

                // tangent
                if (mesh->MTangents != null)
                {
                    vertex.Tangent = mesh->MTangents[i];
                }

                // bitangent
                if (mesh->MBitangents != null)
                {
                    vertex.Bitangent = mesh->MBitangents[i];
                }

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
                if (face.MNumIndices == 3)
                {
                    for (uint j = 0; j < face.MNumIndices; j++)
                    {
                        indices.Add(face.MIndices[j]);
                    }
                }
                else
                {
                    int a = 1;
                }
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
                /*
                 * For this mesh, there a bones given that influence the mesh.
                 * So collect the information about each individual bone.
                 * This gathers the vertex and weight infos of each of the bones.
                 */
                BoneMesh[] boneMeshes = new BoneMesh [mesh->MNumBones];
                var skeleton = _model.FindSkeleton();

                for (int i = 0; i < mesh->MNumBones; ++i)
                {
                    var aiBone = mesh->MBones[i];

                    var jBone = skeleton.FindBone(aiBone->MName.ToString());
                    jBone.Model2Bone = Matrix4x4.Transpose(aiBone->MOffsetMatrix);
                    jBone.Bone2Model = MatrixInversion.Invert(jBone.Model2Bone);

                    var nBoneVertices = aiBone->MNumWeights;
                    boneMeshes[i] = new BoneMesh(jBone, nBoneVertices);

                    for (int j = 0; j < nBoneVertices; ++j)
                    {
                        boneMeshes[i].SetVertexWeight(aiBone->MWeights[j].MVertexId, aiBone->MWeights[j].MWeight);
                    }
                }

                // TXWTODO: MNumBones seems to contain most of the bones and not just the ones relevant for this mesh.

                /*
                 * Now read the first nBones bones back into the influence lists for each of the
                 * vertices.
                 */
                jMesh.BoneIndices = new List<Int4>(new Int4[nMeshVertices]);
                for (int j = 0; j < nMeshVertices; ++j)
                {
                    jMesh.BoneIndices[j] = new Int4(-1);
                }

                jMesh.BoneWeights = new List<Vector4>(new Vector4[nMeshVertices]);
                uint maxBoneIndex = 0;
                for (int j = 0; j < mesh->MNumBones; j++)
                {
                    var boneMesh = boneMeshes[j];

                    uint boneIndex = boneMesh.Bone.Index;
                    int nBoneVertices = (boneMesh.VertexWeights != null) ? (boneMesh.VertexWeights.Length) : 0;
                    maxBoneIndex = UInt32.Max(boneIndex, maxBoneIndex);
                    for (int k = 0; k < nBoneVertices; ++k)
                    {
                        Int4 i4;
                        Vector4 w4;

                        ref VertexWeight vw = ref boneMesh.VertexWeights[k];
                        int vertexIndex = (int)vw.VertexIndex;
                        float weight = vw.Weight;

                        i4 = jMesh.BoneIndices[vertexIndex];
                        w4 = jMesh.BoneWeights[vertexIndex];

                        int l;
                        for (l = 0; l < 4; ++l)
                        {
                            if (-1 == i4[l])
                            {
                                i4[l] = (int)boneIndex;
                                w4[l] = weight;
                                break;
                            }
                        }

                        if (4 == l)
                        {
                            int minL = -1;
                            float minW = Single.MaxValue;
                            for (l = 0; l < 4; ++l)
                            {
                                if (w4[l] < minW)
                                {
                                    minL = l;
                                    minW = w4[l];
                                }
                            }

                            if (minL != -1)
                            {
                                w4[minL] = weight;
                                i4[minL] = (int)boneIndex;
                            }
                            else
                            {
                                int a = 1;
                            }
                        }

                        jMesh.BoneIndices[vertexIndex] = i4;
                        jMesh.BoneWeights[vertexIndex] = w4;
                    }
                }


                /*
                 * Finally, normalize the weights
                 */
                for (int j = 0; j < nMeshVertices; ++j)
                {
                    float totalWeight = 0f;
                    var i4 = jMesh.BoneIndices[j];
                    var w4 = jMesh.BoneWeights[j];
                    for (int l = 0; l < 4; ++l)
                    {
                        if (i4[l] != -1)
                        {
                            totalWeight += w4[l];
                        }
                    }

                    if (totalWeight != 0f)
                    {
                        float inv = 1f / totalWeight;
                        for (int l = 0; l < 4; ++l)
                        {
                            if (i4[l] != -1)
                            {
                                w4[l] *= inv;
                            }
                        }
                    }

                    jMesh.BoneIndices[j] = i4;
                    jMesh.BoneWeights[j] = w4;

                }
            }

            return jMesh;
        }
        catch (Exception e)
        {
            Error($"Exception processing mesh; {e}");
            return null;    
        }
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
            vertices.Add(vertex.Normal.X);
            vertices.Add(vertex.Normal.Y);
            vertices.Add(vertex.Normal.Z);
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