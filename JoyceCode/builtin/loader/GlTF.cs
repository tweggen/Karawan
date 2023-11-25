using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using engine.joyce;
using glTFLoader;
using glTFLoader.Schema;
using static engine.Logger;

namespace builtin.loader;

public class GlTF
{
    static public void LoadModelInstanceSync(string url,
        ModelProperties modelProperties,
        out engine.joyce.InstanceDesc instanceDesc, out engine.ModelInfo modelInfo)
    {
        instanceDesc = new(
            new List<engine.joyce.Mesh>(),
            new List<int>(),
            new List<engine.joyce.Material>(),
            400f);

        modelInfo = new();

        Gltf? model = null;
        byte[]? binary = null;
        using (var fileStream = engine.Assets.Open(url))
        {
            try
            {
                model = Interface.LoadModel(fileStream);
            }
            catch (Exception e)
            {
                Error($"Unable to load json from gltf file: Exception: {e}");
            }
        }

        using (var fileStream = engine.Assets.Open(url))
        {
            try
            {
                binary = Interface.LoadBinaryBuffer(fileStream);
            }
            catch (Exception e)
            {
                Error($"Unable to load binaries from gltf file: Exception: {e}");
            }
        }

        if (model == null)
        {
            Warning($"Error loading model {url}.");
        }
        
        /*
         * Simple approach, try to extract all the meshses.
         * Unfortunately, we have to push the optimized gltf data structures into
         * our generic ones before actually uploading them to GL.
         */
        foreach (var fbxMesh in model.Meshes)
        {
            foreach (var fbxMeshPrimitive in fbxMesh.Primitives)
            {
                int idxPosition = -1;
                int idxNormal = -1;
                int idxTexcoord0 = -1;

                /*
                 * Collect the attributes
                 */
                foreach (var fbxAttr in fbxMeshPrimitive.Attributes)
                {
                    switch (fbxAttr.Key)
                    {
                        case "POSITION":
                            idxPosition = fbxAttr.Value;
                            break;
                        case "NORMAL":
                            idxNormal = fbxAttr.Value;
                            break;
                        case "TEXCOORD_0":
                            idxTexcoord0 = fbxAttr.Value;
                            break;
                        default:
                            break;
                    }
                }

                if (idxPosition == -1)
                {
                    /*
                     * A mesh without vertex positions is pointless.
                     */
                    continue;
                }

                if (null == fbxMeshPrimitive.Indices)
                {
                    /*
                     * Also, without an index we do not need to access this mesh.
                     */
                    continue;
                }

                /*
                 * Now let's iterate through the vertex positions, adding normals and
                 * texcoords, if we have any.
                 */
                var accVertices = model.Accessors[idxPosition];
                var accNormals = model.Accessors[idxNormal];
                var accTexcoord0 = model.Accessors[idxTexcoord0];
                var accIndices = model.Accessors[fbxMeshPrimitive.Indices.Value];

                var bvwVertices = model.BufferViews[accVertices.BufferView.Value];
                var bvwNormals = model.BufferViews[accNormals.BufferView.Value];
                var bvwTexcoord0 = model.BufferViews[accTexcoord0.BufferView.Value];
                var bvwIndices = model.BufferViews[accIndices.BufferView.Value];

                
                /*
                 * Now first let's translate the vertices, then let's convert/add the indices.
                 */
                engine.joyce.Mesh jMesh = new("gltf", new List<Vector3>(), new List<uint>(), new List<Vector2>());
            }
        }
    }
    
    
    public static void Unit()
    {
        LoadModelInstanceSync("u.glb", 
            new ModelProperties()
            , out var _, out var _);
    }
}