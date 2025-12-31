using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using builtin.extensions;
using engine;
using engine.elevation;
using engine.joyce;
using engine.joyce.components;
using FbxSharp;
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
    private Metadata _metadata;
    private AxisInterpreter _axi = new(
#if false
        Vector3.UnitX,
        Vector3.UnitY, 
        Vector3.UnitZ
#else
        Vector3.UnitX,
        Vector3.UnitY,
        -Vector3.UnitZ
#endif
    );
    
    /**
     * The axis interpreter for loading animation.
     */
    private AxisInterpreter _baxi = new AxisInterpreter(
        #if false
        Vector3.UnitX, 
        Vector3.UnitY, 
        Vector3.UnitZ
        #else
        Vector3.UnitX,
        Vector3.UnitY,
        -Vector3.UnitZ
        #endif
        );

    private static object _slo = new();
    
    private Matrix4x4 _fbxTranspose(in Matrix4x4 m) => Matrix4x4.Transpose(m);

    private bool _traceFbxTree = false;
    private bool _traceFbxMetadata = false;
    
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
        if (!_equalsRoughly(meshNode->MTransformation, animNode->MTransformation)) {
            ErrorThrow<InvalidOperationException>($"Transformation differs for node: {meshNode->MName}\n");
        }
    }
    

    static void NormalizeTimes<T>(KeyFrame<T>[]? keys, float duration)
    {
        if (keys == null || keys.Length == 0)
            return;

        for (int k = 0; k < keys.Length; ++k)
        {
            float t = keys[k].Time;

            // Wrap into [0, duration)
            t = t % duration;
            if (t < 0)
                t += duration;

            keys[k].Time = t;
        }

        Array.Sort(keys, (a, b) => a.Time.CompareTo(b.Time));
    }
    

    static bool IsRestPoseRotation(Quaternion q, Quaternion rest, bool verbose)
    {
        float dot = Quaternion.Dot(q, rest);
        if (verbose)
        {
            Trace($"Dot product was {dot}");
        }

        
        // Allow small deviation; adjust threshold if needed
        if (dot > 0.9999f)
        {
            return true;
        }
        else
        {
            return false;
        }
    }
    

    static void RemoveRestPoseRotationKeys(KeyFrame<Quaternion>[]? keys, Quaternion restRot, bool verbose = false)
    {
        if (keys == null || keys.Length < 2)
            return;

        // We keep the FIRST key at t=0 that is NOT rest pose.
        // All other t=0 rest-pose keys are removed.

        List<KeyFrame<Quaternion>> result = new(keys.Length);

        bool foundNonRestPoseZero = false;

        foreach (var k in keys)
        {
            if (k.Time == 0f)
            {
                if (!IsRestPoseRotation(k.Value, restRot, verbose))
                {
                    // Keep the first non-rest-pose key at t=0
                    if (!foundNonRestPoseZero)
                    {
                        result.Add(k);
                        foundNonRestPoseZero = true;
                    }
                    // If we already kept one, drop duplicates at t=0
                }
                else
                {
                    // Drop rest-pose keys at t=0
                    continue;
                }
            }
            else
            {
                // Normal key, keep it
                result.Add(k);
            }
        }

        // Copy back into the original array
        for (int i = 0; i < result.Count; ++i)
            keys[i] = result[i];

        // Clear the rest
        for (int i = result.Count; i < keys.Length; ++i)
            keys[i] = default;
    }


    /*
     * Given the rest pose matrix, extract the rotation from it.
     */
    static Quaternion ExtractRestPoseRotation(ModelNode node)
    {
        return Quaternion.CreateFromRotationMatrix(node.Transform.Matrix);
    }
            
            
    /**
     * Load all animations found in this assimp scene.
     */
    private unsafe void _loadAnimations(string strFallbackName, Scene* scene, ModelNode mnRestPose)
    {
        if (scene->MAnimations == null)
            return;

        uint nAnimations = scene->MNumAnimations;
        if (nAnimations == 0)
            return;

        for (int i = 0; i < nAnimations; ++i)
        {
            var aiAnim = scene->MAnimations[i];
            if (aiAnim == null || aiAnim->MNumChannels == 0)
                continue;

            uint nChannels = aiAnim->MNumChannels;

            ModelAnimation ma = _model.AnimationCollection.CreateAnimation(mnRestPose);
            ma.Name = string.IsNullOrWhiteSpace(strFallbackName)
                ? aiAnim->MName.ToString()
                : strFallbackName;

            ma.Duration = (float)aiAnim->MDuration / (float)aiAnim->MTicksPerSecond;
            ma.TicksPerSecond = (float)aiAnim->MTicksPerSecond;
            ma.NTicks = (uint)aiAnim->MDuration;
            ma.MapChannels = new();

            uint nFrames = UInt32.Max((uint)(ma.Duration * 60f), 1);
            ma.NFrames = nFrames;
            _model.AnimationCollection.PushAnimFrames(nFrames);

            if (ma.Name.StartsWith("Walk_InPlace_Female"))
            {
                int a = 1;
            }

            /*
             * Before going through the channels, try to identify the identity frame.
             *
             * So first count the keyframes...
             */
            uint nKeyframes = 0;
            for (int j = 0; j < nChannels; ++j)
            {
                var aiChannel = aiAnim->MChannels[j];
                if (aiChannel == null)
                    continue;

                string channelNodeName = aiChannel->MNodeName.ToString();

                if (!_model.Skeleton.MapBones.ContainsKey(channelNodeName) ||
                    !_model.ModelNodeTree.MapNodes.ContainsKey(channelNodeName))
                    continue;

                ModelNode channelNode = _model.ModelNodeTree.MapNodes[channelNodeName];

                if (ma.MapChannels.ContainsKey(channelNode))
                {
                    continue;
                }

                nKeyframes = UInt32.Max(nKeyframes, aiChannel->MNumPositionKeys);
                nKeyframes = UInt32.Max(nKeyframes, aiChannel->MNumRotationKeys);
                nKeyframes = UInt32.Max(nKeyframes, aiChannel->MNumScalingKeys);
            }

            /*
             * Now count again, if all channels really have the same number of keyframes.
             */
            for (int j = 0; j < nChannels; ++j)
            {
                var aiChannel = aiAnim->MChannels[j];
                if (aiChannel == null)
                    continue;

                string channelNodeName = aiChannel->MNodeName.ToString();

                if (!_model.Skeleton.MapBones.ContainsKey(channelNodeName) ||
                    !_model.ModelNodeTree.MapNodes.ContainsKey(channelNodeName))
                    continue;

                ModelNode channelNode = _model.ModelNodeTree.MapNodes[channelNodeName];

                if (ma.MapChannels.ContainsKey(channelNode))
                {
                    continue;
                }

                #if false
                if (aiChannel->MNumPositionKeys != nKeyframes)
                {
                    Warning($"Channel {aiChannel->MNodeName} has different number of position keyframes: {aiChannel->MNumPositionKeys} != {nKeyframes}");
                }
                if (aiChannel->MNumRotationKeys != nKeyframes)
                {
                    Warning($"Channel {aiChannel->MNodeName} has different number of rotation keyframes: {aiChannel->MNumRotationKeys} != {nKeyframes}");
                }
                if (aiChannel->MNumScalingKeys != nKeyframes)
                {
                    Warning($"Channel {aiChannel->MNodeName} has different number of scaling keyframes: {aiChannel->MNumScalingKeys} != {nKeyframes}");
                }
                #endif
            }
        

            
            #if true
            SortedDictionary<double, int> mapRestCandidates = new();

            /*
             * for each channel, find the original time of the keyframe that resembles
             * the rest pose the most.
             * Note, that the keyframe might live at a different index for each of the
             * frames.
             */
            for (int j = 0; j < nChannels; ++j)
            {
                var aiChannel = aiAnim->MChannels[j];
                if (aiChannel == null)
                    continue;

                string channelNodeName = aiChannel->MNodeName.ToString();

                if (!_model.Skeleton.MapBones.ContainsKey(channelNodeName) ||
                    !_model.ModelNodeTree.MapNodes.ContainsKey(channelNodeName))
                    continue;

                ModelNode node = _model.ModelNodeTree.MapNodes[channelNodeName];

                if (ma.MapChannels.ContainsKey(node))
                {
                    continue;
                }

                var restRotation = Quaternion.CreateFromRotationMatrix(node.Transform.Matrix);
                double timeChannelsRestFrame = 0f;
                float maxValue = Single.MinValue;
                int nZeroes = 0;
                for (int k = 0; k < nKeyframes; ++k)
                {
                    /*
                     * It needs to be at time position 0. Hopefully.
                     */
                    var normalizedTime = aiChannel->MRotationKeys[k].MTime % aiAnim->MDuration;
                    if (normalizedTime < 0)
                    {
                        normalizedTime += aiAnim->MDuration;
                    }
                    if (normalizedTime != 0) continue;

                    /*
                     * In the end, there must be more than one zero in the run.
                     */
                    ++nZeroes;
                    
                    /*
                     * And it needs to the largest dot product.
                     */
                    float dot = Quaternion.Dot(aiChannel->MRotationKeys[k].MValue, restRotation);
                    if (dot > maxValue)
                    {
                        maxValue = dot;
                        
                        /*
                         * note this is the unwrapped untranslated time.
                         */
                        timeChannelsRestFrame = aiChannel->MRotationKeys[k].MTime;
                    }
                }

                if (nZeroes > 1)
                {
                    if (mapRestCandidates.TryGetValue(timeChannelsRestFrame, out var nFoundUpToNow))
                    {
                        mapRestCandidates[timeChannelsRestFrame] = nFoundUpToNow + 1;
                    } 
                    else
                    {
                        mapRestCandidates[timeChannelsRestFrame] = 1;
                    }
                }
            }
            #endif
            
            /*
             * Now find out the timestamp with the most results.
             */ 
            double timeRestFrame = Double.MinValue;
            int maxCount = 0;
            foreach (var kv in mapRestCandidates)
            {
                if (kv.Value > maxCount)
                {
                    maxCount = kv.Value;
                    timeRestFrame = kv.Key;
                }
            }
            

            Trace( $"Detected rest frame @{timeRestFrame} in animation {ma.Name} with {maxCount} votes.");
            
            for (int j = 0; j < nChannels; ++j)
            {
                var aiChannel = aiAnim->MChannels[j];
                if (aiChannel == null)
                    continue;

                string channelNodeName = aiChannel->MNodeName.ToString();

                if (!_model.Skeleton.MapBones.ContainsKey(channelNodeName) ||
                    !_model.ModelNodeTree.MapNodes.ContainsKey(channelNodeName))
                    continue;

                ModelNode channelNode = _model.ModelNodeTree.MapNodes[channelNodeName];

                if (ma.MapChannels.ContainsKey(channelNode))
                {
                    continue;
                }

                uint nPositionKeys = aiChannel->MNumPositionKeys;
                uint nScalingKeys  = aiChannel->MNumScalingKeys;
                uint nRotationKeys = aiChannel->MNumRotationKeys;

                if (nPositionKeys == 0 && nScalingKeys == 0 && nRotationKeys == 0)
                {
                    continue;
                }

                ModelAnimChannel mac = ma.CreateChannel(
                    channelNode,
                    nPositionKeys != 0 ? new KeyFrame<Vector3>[nPositionKeys]     : null,
                    nRotationKeys != 0 ? new KeyFrame<Quaternion>[nRotationKeys] : null,
                    nScalingKeys  != 0 ? new KeyFrame<Vector3>[nScalingKeys]      : null
                );

                float duration = ma.Duration;

                // Load raw keyframes
                for (int l = 0; l < nPositionKeys; ++l)
                {
                    mac.Positions![l] = new KeyFrame<Vector3>
                    {
                        Time  = (float)aiChannel->MPositionKeys[l].MTime / ma.TicksPerSecond,
                        OrgTime = (float)aiChannel->MPositionKeys[l].MTime,
                        Value = _baxi.ToJoyce(aiChannel->MPositionKeys[l].MValue)
                    };
                }

                for (int l = 0; l < nScalingKeys; ++l)
                {
                    mac.Scalings![l] = new KeyFrame<Vector3>
                    {
                        Time  = (float)aiChannel->MScalingKeys[l].MTime / ma.TicksPerSecond,
                        OrgTime = (float)aiChannel->MPositionKeys[l].MTime,
                        Value = _baxi.ToJoyceScale(aiChannel->MScalingKeys[l].MValue)
                    };
                }

                for (int l = 0; l < nRotationKeys; ++l)
                {
                    mac.Rotations![l] = new KeyFrame<Quaternion>
                    {
                        Time  = (float)aiChannel->MRotationKeys[l].MTime / ma.TicksPerSecond,
                        OrgTime = (float)aiChannel->MPositionKeys[l].MTime,
                        Value = _baxi.ToJoyce(aiChannel->MRotationKeys[l].MValue)
                    };
                }

                #if false
                if (ma.Name.StartsWith("Walk_InPlace_Female") &&
                    mac.Target.Name.StartsWith("Shoulder_L"))
                {
                    int a = 1;
                }
                #endif

                /*
                 * Remove rest frame at thje given time position.
                 */
                if (timeRestFrame != Double.MinValue)
                {
                    var tmpPositions = mac.Positions.ToList();
                    tmpPositions.RemoveAll(kf => kf.OrgTime == timeRestFrame);
                    mac.Positions = tmpPositions.ToArray();
                    
                    var tmpRotations = mac.Rotations.ToList();
                    tmpPositions.RemoveAll(kf => kf.OrgTime == timeRestFrame);
                    mac.Rotations = tmpRotations.ToArray();
                    
                    var tmpScalings = mac.Scalings.ToList();
                    tmpPositions.RemoveAll(kf => kf.OrgTime == timeRestFrame);
                    mac.Scalings = tmpScalings.ToArray();
                }
                
                if (ma.Name.StartsWith("Walk_InPlace_Female"))
                {
                    int a = 1;
                }
                /*
                 * Normalize times into [0, duration) and sort
                 */
                NormalizeTimes(mac.Positions, duration);
                NormalizeTimes(mac.Scalings,  duration);
                NormalizeTimes(mac.Rotations, duration);

                ma.MapChannels[channelNode] = mac;
                mac.Target = channelNode;
            }
            
            if (ma.Name.StartsWith("Walk_InPlace_Female"))
            {
                int a = 1;
            }

            _model.AnimationCollection.MapAnimations[ma.Name] = ma;
        }
    }


    #if false
    private unsafe void _loadAnimations(string strFallbackName, Scene *scene, ModelNode mnRestPose)
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

            ModelAnimation ma = _model.AnimationCollection.CreateAnimation(mnRestPose);
            ma.Name = String.IsNullOrWhiteSpace(strFallbackName)?aiAnim->MName.ToString():strFallbackName;
            Trace($"Found Animation \"{ma.Name}\" with {nChannels} channels.");
            if (ma.Name.StartsWith("Run_InPlace"))
            {
                int a = 1;
            }
            ma.Duration = (float)aiAnim->MDuration / (float)aiAnim->MTicksPerSecond;
            ma.TicksPerSecond = (float)aiAnim->MTicksPerSecond;
            ma.NTicks = (uint)(aiAnim->MDuration);
            ma.MapChannels = new();
            uint nFrames = UInt32.Max((uint)(ma.Duration * 60f), 1);
            ma.NFrames = nFrames;
            _model.AnimationCollection.PushAnimFrames(nFrames);

            for (int j = 0; j < nChannels; ++j)
            {
                var aiChannel = aiAnim->MChannels[j];
                if (null == aiChannel)
                {
                    continue;
                }
                
                string channelNodeName = aiChannel->MNodeName.ToString();
                // Trace($"Animation \"{ma.Name}\" controls channel: {channelNodeName}");
                if (!_model.Skeleton.MapBones.ContainsKey(channelNodeName))
                {
                    Warning($"Found animation channel for unknown bone {channelNodeName}, ignoring.");
                    continue;
                }
                if (!_model.ModelNodeTree.MapNodes.ContainsKey(channelNodeName))
                {
                    Warning($"Found animation channel for unknown node {channelNodeName}, ignoring.");
                    continue;
                }
                ModelNode channelNode = _model.ModelNodeTree.MapNodes[channelNodeName];

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
                        Value = _baxi.ToJoyce(aiChannel->MPositionKeys[l].MValue)
                    };
                }
                
                for (int l = 0; l < nScalingKeys; ++l)
                {
                    mac.Scalings![l] = new()
                    {
                        Time = (float)aiChannel->MScalingKeys[l].MTime / ma.TicksPerSecond,
                        Value = _baxi.ToJoyceScale(aiChannel->MScalingKeys[l].MValue)
                    };
                }
                
                for (int l = 0; l < nRotationKeys; ++l)
                {
                    mac.Rotations![l] = new()
                    {
                        Time = (float)aiChannel->MRotationKeys[l].MTime / ma.TicksPerSecond,
                        Value = _baxi.ToJoyce(aiChannel->MRotationKeys[l].MValue)
                    };
                }
                
                ma.MapChannels[channelNode] = mac;
                mac.Target = channelNode;
            }

            _model.AnimationCollection.MapAnimations[ma.Name] = ma;
        } 
    }
    #endif


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
        var diffuseMaps = _loadMaterialTextures(aiMat, TextureType.Diffuse, "texture_diffuse");
        
        /*if (diffuseMaps.Any())
            textures.AddRange(diffuseMaps);*/
        // 2. specular maps
        var specularMaps = _loadMaterialTextures(aiMat, TextureType.Specular, "texture_specular");
        /*if (specularMaps.Any())
            textures.AddRange(specularMaps);*/
        // 3. normal maps
        var normalMaps = _loadMaterialTextures(aiMat, TextureType.Height, "texture_normal");
        /*if (normalMaps.Any())
            textures.AddRange(normalMaps);*/
        // 4. height maps
        var heightMaps = _loadMaterialTextures(aiMat, TextureType.Ambient, "texture_height");
        /*if (heightMaps.Any())
            textures.AddRange(heightMaps);*/

        // new() { Texture = I.Get<TextureCatalogue>().FindColorTexture(0xff888888) };
        engine.joyce.Material jMaterial = new();
        if (diffuseMaps.Any())
        {
            var path = diffuseMaps[0].Path;
            engine.joyce.Texture? jTexture = null;
            
            /*
             * We may have windows relative pathes, which would not work on a unix system,
             * so do a last resort conversion here. This forbids file names with backslashes
             * on unix machines. But, come on, man.
             */
            if (Path.DirectorySeparatorChar == '/')
            {
                path = path.Replace('\\', '/');
            }
            
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
                    Path.GetFileName(path),null, out jTexture))
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
    
    
    /**
     * Iterate through the hierarchy of assimp nodes. Depending on the mode of operation, only load and
     * create the nodes
     * - containing the mesh
     * - containg other data
     * Return the corresponding model node and wether we had a mesh below of us.
     */
    private unsafe engine.joyce.ModelNode? _processNode(
        ModelNode mnParent,
        Node* node, MergePolicy mp,
        out bool meshInOrBelowMe)
    {
        string strName = node->MName.ToString();
        meshInOrBelowMe = false;
        var skeleton = _model!.FindSkeleton(); 

        /*
         * We need a model node to load our data into.
         * We start by creating a temporary node. After loading we
         * decide if we want to override an existing node.
         */
        bool couldOverrideNode = false;
        bool iHaveMesh = false;

        ModelNode mn = _model.ModelNodeTree.CreateNode(_model);
        mn.Name = strName;

        if (_model!.ModelNodeTree.MapNodes.ContainsKey(strName))
        {
            /*
             * We silently assume the similarily named node also had the same parent.
             */
            couldOverrideNode = true;
        }
        
        /*
         * If there are meshes, add them.
         * We do not support adding meshes. 
         */
        if (mp.LoadMeshes && node->MNumMeshes > 0)
        {
            if (couldOverrideNode)
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
         * If there are children, parse them according to load mode.
         * Remember, we are loading into a temporary node.
         * The children will later be merged with the real tree.
         */
        if (node->MNumChildren > 0)
        {
            for (var i = 0; i < node->MNumChildren; i++)
            {
                var aiChild = node->MChildren[i];
                var mnChild = _processNode(mn, aiChild, mp, out var childOrBelowHasMesh);
                if (null != mnChild)
                {
                    meshInOrBelowMe |= childOrBelowHasMesh;
                
                    mn.AddChild(mnChild);
                    
                } 
                else
                {
                    /*
                     * If the child is irrelevant in the context of the call
                     * we skip it.
                     */

                }
            }
        }

        #if false
        if (node->MMetaData != null && node->MMetaData->MNumProperties > 0)
        {
            Metadata metaNode = new(node->MMetaData);
            Trace($"Node {mn.Name} has metadata:");
            metaNode.Dump();
        }
        #endif
        
        /*
         * Look if we want to store the model node we wrote, either by adding it to a
         * parent or by overriding an existing node. 
         *
         * We would do so, if
         * - I am supposed to load the mesh and any of my children contains a mesh.
         * - If I am supposed to load the main nodes, store my matrix as well.
         */
        if ((mp.LoadMeshes && meshInOrBelowMe) || mp.LoadMainNodes)
        {
            var m4ToParent = _axi.ToJoyce(_fbxTranspose(node->MTransformation));
            mn.Transform = new Transform3ToParent(true, 0xffffffff, m4ToParent);
            
            return mn;

        }
        else
        {
            return null;
        }
    }


    private static bool _equalsRoughly(in Matrix4x4 a, in Matrix4x4 b)
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

                vertex.Position = _axi.ToJoyce(mesh->MVertices[i]);
                
                // normals
                if (mesh->MNormals != null)
                {
                    if (_axi.IsLeftHanded)
                    {
                        int a = 1;
                        
                    }
                    vertex.Normal = _axi.ToJoyceNormal(mesh->MNormals[i]);
                }

                // tangent
                if (mesh->MTangents != null)
                {
                    vertex.Tangent = _axi.ToJoyce(mesh->MTangents[i]);
                }

                // bitangent
                if (mesh->MBitangents != null)
                {
                    vertex.Bitangent = _axi.ToJoyce(mesh->MBitangents[i]);
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
                    if (_axi.IsLeftHanded)
                    {
                        for (int j = (int)face.MNumIndices-1; j >= 0; j--)
                        {
                            indices.Add(face.MIndices[j]);
                        }
                    }
                    else
                    {
                        for (uint j = 0; j < face.MNumIndices; j++)
                        {
                            indices.Add(face.MIndices[j]);
                        }
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
            var fbxMesh = new Mesh(_buildVertices(vertices), _buildIndices(indices) /* , textures */);
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
                    jBone.Model2Bone = _axi.ToJoyce(_fbxTranspose(aiBone->MOffsetMatrix));
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
                int maxBoneIndex = 0;
                for (int j = 0; j < mesh->MNumBones; j++)
                {
                    var boneMesh = boneMeshes[j];

                    int boneIndex = boneMesh.Bone.Index;
                    int nBoneVertices = (boneMesh.VertexWeights != null) ? (boneMesh.VertexWeights.Length) : 0;
                    maxBoneIndex = Int32.Max(boneIndex, maxBoneIndex);
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
    

    private unsafe List<Texture> _loadMaterialTextures(Material* mat, TextureType type, string typeName)
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

    
    private float[] _buildVertices(List<Vertex> vertexCollection)
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

    
    private uint[] _buildIndices(List<uint> indices)
    {
        return indices.ToArray();
    }


    /**
     * I am a non-pivot thing. Merge all parents with the same beginning as my name into me.
     */
    private void _mergeParentsAssimpPivotsIntoMe(ModelNode mn)
    {
        Matrix4x4 m4Total = mn.Transform.Matrix;

        var strPivotStem = $"{mn.Name}_$AssimpFbx$_";
        for (ModelNode mnPivot = mn.Parent; mnPivot != null; mnPivot = mnPivot.Parent)
        {
            if (mnPivot.Name == null || !mnPivot.Name.StartsWith(strPivotStem))
            {
                break;
            }

            if (mnPivot.Name.EndsWith("_Translation"))
            {
                m4Total = mnPivot.Transform.Matrix * m4Total;
            }
            else
            {
                m4Total = mnPivot.Transform.Matrix * m4Total;
            }
            mnPivot.Transform.Matrix = Matrix4x4.Identity;
        }
        mn.Transform.Matrix = m4Total;
    }

    private void _applyScalingToModel(Model model, Metadata metadata, float scale)
    {
        var strUnitscale = _metadata.GetString("UnitScaleFactor", "1.");
        float unitscale = float.Parse(strUnitscale, CultureInfo.InvariantCulture);
        model.Scale = unitscale / 100f * scale;
    }
    
    private void _applyScalingToRootNode(ModelNode mnRoot, Metadata metadata, float scale)
    {
        var strUnitscale = _metadata.GetString("UnitScaleFactor", "1.");
        float unitscale = float.Parse(strUnitscale, CultureInfo.InvariantCulture);
        float totalScale = unitscale / 100f * scale;
        mnRoot.Transform.Matrix = 
            Matrix4x4.CreateScale(totalScale) * 
            mnRoot.Transform.Matrix;
    }
    
    /**
     * Iterate through all model nodes, removing transformations from
     * *_$AssimpFbx$_Translation nodes into the first child with same name, without assimp postfix.
     */
    private void _mergeAssimpPivotsRecursively(ModelNode mn)
    {
        if (mn.Name != null && !mn.Name!.Contains($"_$AssimpFbx$_"))
        {
            _mergeParentsAssimpPivotsIntoMe(mn);
        }

        if (mn.Children != null)
        {
            foreach (var mnChild in mn.Children)
            {
                _mergeAssimpPivotsRecursively(mnChild);
            }
        }
    }
    
 
    /**
     * Load a given fbx file into this model.
     * You can also pass additional files to add e.g. animation data.
     */
    public unsafe void Load(string path,
        List<string>? additionalUrls,
        string? strModelBaseBone,
        List<string>? cpuNodes,
        float scale, 
        AxisInterpreter? axisInterpreter,
        AxisInterpreter? animAxisInterpreter,
        out engine.joyce.Model model)
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
        bool haveAdditionalFiles = !(additionalUrls == null || additionalUrls.Count == 0);
        bool loadMainAnimations = !haveAdditionalFiles;
        bool loadMainNodes = true;

        /*
         * Prepare data structures.
         */
        _model = model = new engine.joyce.Model()
        {
            ModelUrl = path,
            AnimationUrls = additionalUrls != null ? string.Join(";", additionalUrls) : null
        };
        _model.Name = path;
        _model.AnimationCollection.MapAnimations = new();

        /*
         * Load the actual file.
         */
        
        Directory = path;
        _needAssimp();
        
        FileIO fileIO = fbx.Assets.Get();
        FileIO* pFileIO = &fileIO;
        PropertyStore *properties = _assimp.CreatePropertyStore();
        // TXWTODO: Does not work.
        _assimp.SetImportPropertyInteger(properties, "AI_CONFIG_IMPORT_FBX_PRESERVE_PIVOTS", 0);
        _assimp.SetImportPropertyInteger(properties, "AI_CONFIG_IMPORT_REMOVE_EMPTY_BONES", 0);
        _assimp.SetImportPropertyInteger(properties, "AI_CONFIG_IMPORT_FBX_IGNORE_UP_DIRECTION", 1);
        _assimp.ReleaseImport(_scene);
        _scene = _assimp.ImportFileExWithProperties(
            path,
            (uint)PostProcessSteps.Triangulate,
            pFileIO,
            properties
        );
        Trace($"Loaded \"{path}\"");
        _metadata = new(_scene->MMetaData);
        if (_traceFbxMetadata)
        {
            _metadata.Dump();
        }

        if (axisInterpreter != null)
        {
            _axi = axisInterpreter;
            _baxi = axisInterpreter;
        }

        if (animAxisInterpreter != null)
        {
            _baxi = animAxisInterpreter;
        }
        
        
        if (_scene == null || _scene->MFlags == Assimp.SceneFlagsIncomplete || _scene->MRootNode == null)
        {
            var error = _assimp.GetErrorStringS();
            throw new Exception(error);
        }

        ModelNode? mnPoseRoot = _processNode(null, _scene->MRootNode,
            new MergePolicy()
            {
                LoadMainNodes = loadMainNodes,
                LoadMeshes = true
            },
            out var _); 
        /*
         * Remove transformations of pivots in case assimp did not merge it.
         */
        _mergeAssimpPivotsRecursively(mnPoseRoot);
        model.ModelNodeTree.SetRootNode(mnPoseRoot, model.FindSkeleton());
        _applyScalingToRootNode(model.ModelNodeTree.RootNode, _metadata, scale);
        _applyScalingToModel(model, _metadata, scale);

        if (_traceFbxTree)
        {
            Trace("Pose model:");
            Trace(model.ModelNodeTree.RootNode.DumpNode());
        }

        /*
         * Now load all the animations. First the ones from the main file.
         */

        if (loadMainAnimations)
        {
            /*
             * Note, that if we load the main animations, we also already had loaded the main nodes,
             * i.e. the bones.
             */
            _loadAnimations("", _scene, null);
        }

        bool haveLoadedBakedAnimations = false;

        /*
         * Now go through the extra fbx files and load the animations from
         * them to this model.
         */
        if (additionalUrls != null)
        {
            /*
             * Before loading the additional urls, check, if we have a baked animation file
             * for all of them.
             */
            if (false) try
            {
                haveLoadedBakedAnimations = model.TryLoadModelAnimationCollection(out var animcoll);
                if (haveLoadedBakedAnimations && animcoll != null)
                {
                    if (model.AnimationCollection.TestBakedAnimationsFrom(animcoll))
                    {
                        model.AnimationCollection.UseBakedAnimationsFrom(animcoll);
                    }
                }
            }
            catch (Exception e)
            {
                Warning($"Unable to import baked animations: {e}");
                haveLoadedBakedAnimations = false;
            }

            if (!haveLoadedBakedAnimations)
            {
                foreach (var url in additionalUrls)
                {
                    try
                    {
                        Trace($"Import additional animation data from {url}...");
                        var additionalScene = _assimp.ImportFileExWithProperties(
                            url,
                            (uint)PostProcessSteps.Triangulate,
                            pFileIO,
                            properties
                        );
                        if (additionalScene == null || additionalScene->MRootNode == null)
                        {
                            continue;
                        }

                        Metadata additionalMetadata = new(additionalScene->MMetaData);
                        if (_traceFbxMetadata)
                        {
                            additionalMetadata.Dump();
                        }

                        _compareBoneHierarchies(additionalScene, _scene);

                        /*
                         * We parse the additional files' children to make sure they match.
                         */
                        MergePolicy mp = new()
                        {
                            LoadMeshes = false,
                            LoadMainNodes = true
                        };
                        ModelNode? mnNewRoot = _processNode(
                            null, additionalScene->MRootNode,
                            mp, out var _);

                        if (null == mnNewRoot) continue;

                        /*
                         * Remove transformations of pivots in case assimp did not merge it.
                         */
                        _mergeAssimpPivotsRecursively(mnNewRoot);

                        _applyScalingToRootNode(mnNewRoot, additionalMetadata, scale);
                        if (_traceFbxTree)
                        {
                            Trace($"Anim {url} model:");
                            Trace($"Model has {additionalScene->MAnimations[0]->MNumChannels} channels.");
                            Trace(mnNewRoot.DumpNode());
                        }

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

                        _loadAnimations(strFallbackName, additionalScene, mnNewRoot);
                        Trace($"Done importing additional animation data from {url}.");
                    }
                    catch (Exception e)
                    {
                        Error($"Exception while loading additional animation data: {e}");
                    }
                }
            }
        }
        _assimp.ReleasePropertyStore(properties);

        if (_metadata.GetString("CustomFrameRate", "-1") == "24")
        {
            //model.WorkAroundInverseRestPose = true;
        }
        else
        {
            //model.WorkAroundInverseRestPose = false;
        }

        /*
         * Set model flags, find first instnace desc 
         */
        model.Polish(strModelBaseBone);

        if (!haveLoadedBakedAnimations)
        {
            try
            {
                /*
                 * Baking animations must include the root matrix corrections.
                 */
                model.BakeAnimations(strModelBaseBone, cpuNodes);
            }
            catch (Exception e)
            {
                Trace($"Caught exception: {e}");
            }
        }
    }
        
        
    public void Dispose()
    {
        _texturesLoaded = null;
    }


    public FbxModel()
    {
    }
}