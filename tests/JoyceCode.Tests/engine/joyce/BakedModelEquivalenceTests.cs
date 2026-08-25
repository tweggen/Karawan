using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using builtin.baking;
using engine.joyce;
using Xunit;
using Xunit.Abstractions;

namespace JoyceCode.Tests.engine.joyce;

/**
 * AC-4.2 / AC-4.3: a baked mo-{hash} model must be the model the fbx loader
 * produces.
 *
 * This is the criterion the whole of Phase 4 rests on, and the plan flags AC-4.3
 * as "the trap": AllBakedMatrices is indexed frame * NBones + boneIndex, so if the
 * model bake and the ac-{hash} animation bake disagree about bone ORDER, every
 * animation renders a foreign pose - plausibly, without crashing. That has already
 * happened once on this codebase. Bone names and order are therefore compared
 * exactly, not approximately.
 *
 * These tests are skipped rather than failed when the repository has not been
 * built (no generated/ directory, no fbx sources), so the suite still runs on a
 * bare checkout.
 */
[Collection("assimp")]
public class BakedModelEquivalenceTests
{
    private const float Tolerance = 1e-6f;

    private readonly ITestOutputHelper _output;

    public BakedModelEquivalenceTests(ITestOutputHelper output)
    {
        _output = output;
    }


    private static DirectoryInfo? _findRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "nogame", "generated"))
                && Directory.Exists(Path.Combine(dir.FullName, "models")))
            {
                return dir;
            }

            dir = dir.Parent;
        }

        return null;
    }


    /**
     * The models declared as "type": "model" in nogame.resources.json, which is
     * the single declaration that decides both what ships and what bakes.
     */
    public static IEnumerable<object[]> DeclaredModels()
    {
        var root = _findRepoRoot();
        if (root == null)
        {
            yield break;
        }

        string resources = Path.Combine(root.FullName, "models", "nogame.resources.json");
        if (!File.Exists(resources))
        {
            yield break;
        }

        System.Text.Json.Nodes.JsonNode? parsed;
        try
        {
            parsed = System.Text.Json.Nodes.JsonNode.Parse(
                File.ReadAllText(resources),
                documentOptions: new System.Text.Json.JsonDocumentOptions
                {
                    CommentHandling = System.Text.Json.JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                });
        }
        catch (Exception)
        {
            yield break;
        }

        /*
         * nogame.resources.json is a satellite that Mix merges in at /resources,
         * so on disk its root IS the resources object and the array is at "list".
         */
        var list = parsed?["list"] as System.Text.Json.Nodes.JsonArray;
        if (list == null)
        {
            yield break;
        }

        foreach (var node in list)
        {
            if ("model" != node?["type"]?.GetValue<string>())
            {
                continue;
            }

            string uri = node["uri"]!.GetValue<string>();
            var properties = new SortedDictionary<string, string>();
            if (node["modelProperties"] is System.Text.Json.Nodes.JsonObject objProperties)
            {
                foreach (var kvp in objProperties)
                {
                    var value = kvp.Value?.GetValue<string>();
                    if (null != value)
                    {
                        properties[kvp.Key] = value;
                    }
                }
            }

            yield return new object[] { uri, properties };
        }
    }


    [Theory]
    [MemberData(nameof(DeclaredModels))]
    public void BakedModelMatchesTheFbxLoader(string uri, SortedDictionary<string, string> properties)
    {
        var root = _findRepoRoot();
        Assert.NotNull(root);

        string modelFileName = Path.GetFileName(uri);
        string bakedPath = Path.Combine(
            root!.FullName, "nogame", "generated", ModelFileName.Of(modelFileName, properties));

        /*
         * The fbx uri in the resource list is relative to models/, and starts with
         * "../models/" because the resource list is itself read from there.
         */
        string fbxPath = Path.GetFullPath(Path.Combine(root.FullName, "models", uri));

        /*
         * Skip only on a genuinely unbuilt checkout - no baked models at all. If
         * the bake HAS run and this particular file is missing, that is the failure
         * this test exists to catch (a bake identity that does not match what the
         * game will ask for), so it must not be silently tolerated. An earlier
         * version of this test returned quietly on any missing file and "passed"
         * all 13 cases in 51 ms without loading a single fbx.
         */
        bool anyBaked = Directory
            .GetFiles(Path.Combine(root.FullName, "nogame", "generated"), "mo-*")
            .Length > 0;
        if (!anyBaked || !File.Exists(fbxPath))
        {
            _output.WriteLine($"skipping {modelFileName}: no bake output present (fbx={File.Exists(fbxPath)})");
            return;
        }

        Assert.True(File.Exists(bakedPath),
            $"{modelFileName}: the bake ran but produced no {Path.GetFileName(bakedPath)}. "
            + "The declared modelProperties and the properties the game loads with must agree - "
            + "they are both inputs to the bake identity.");

        Model baked;
        using (var stream = File.OpenRead(bakedPath))
        {
            baked = ModelReader.Read(stream);
        }

        Assert.NotNull(baked);

        var loaded = AssimpFixture.LoadThroughAssimp(root, modelFileName, properties);
        Assert.NotNull(loaded);

        /*
         * Polish is the loader's last step and is not part of the file, so the
         * baked model gets the same treatment before anything is compared.
         */
        baked.Polish(properties.TryGetValue("ModelBaseBone", out var baseBone) ? baseBone : null);

        _assertSkeletonsMatch(loaded!, baked, modelFileName);
        _assertNodeTreesMatch(loaded!.ModelNodeTree.RootNode, baked.ModelNodeTree.RootNode, modelFileName);
        _assertDerivedTransformsMatch(loaded, baked, modelFileName);
    }


    /**
     * AC-4.3. Names AND order, exactly.
     */
    private void _assertSkeletonsMatch(Model expected, Model actual, string what)
    {
        if (null == expected.Skeleton)
        {
            Assert.Null(actual.Skeleton);
            return;
        }

        Assert.NotNull(actual.Skeleton);
        Assert.Equal(expected.Skeleton.NBones, actual.Skeleton!.NBones);
        Assert.Equal(expected.Skeleton.ListBones.Count, actual.Skeleton.ListBones.Count);

        for (int i = 0; i < expected.Skeleton.ListBones.Count; ++i)
        {
            var e = expected.Skeleton.ListBones[i];
            var a = actual.Skeleton.ListBones[i];

            Assert.True(e.Name == a.Name,
                $"{what}: bone {i} is \"{a.Name}\" in the baked model but \"{e.Name}\" from the fbx. "
                + "AllBakedMatrices is indexed by this position, so a reorder plays the wrong pose.");
            Assert.Equal(e.Index, a.Index);
            _assertMatrixEqual(e.Model2Bone, a.Model2Bone, $"{what}: bone \"{e.Name}\" Model2Bone");
            _assertMatrixEqual(e.Bone2Model, a.Bone2Model, $"{what}: bone \"{e.Name}\" Bone2Model");
        }
    }


    private void _assertNodeTreesMatch(ModelNode expected, ModelNode actual, string what)
    {
        Assert.Equal(expected.Name, actual.Name);
        _assertMatrixEqual(expected.Transform.Matrix, actual.Transform.Matrix,
            $"{what}: node \"{expected.Name}\" transform");
        Assert.Equal(expected.Transform.IsVisible, actual.Transform.IsVisible);
        Assert.Equal(expected.Transform.CameraMask, actual.Transform.CameraMask);

        if (null == expected.InstanceDesc)
        {
            Assert.Null(actual.InstanceDesc);
        }
        else
        {
            Assert.NotNull(actual.InstanceDesc);
            _assertInstanceDescsMatch(expected.InstanceDesc, actual.InstanceDesc!, $"{what}/{expected.Name}");
        }

        if (null == expected.Children)
        {
            Assert.True(null == actual.Children || 0 == actual.Children.Count);
            return;
        }

        Assert.NotNull(actual.Children);
        Assert.Equal(expected.Children.Count, actual.Children!.Count);
        for (int i = 0; i < expected.Children.Count; ++i)
        {
            _assertNodeTreesMatch(expected.Children[i], actual.Children[i], what);
        }
    }


    private void _assertInstanceDescsMatch(InstanceDesc expected, InstanceDesc actual, string what)
    {
        /*
         * ModelTransform and MaxDistance are PER-INSTANCE state that ModelCache
         * applies after loading, and they were the gap that let the double-transform
         * defect ship: the bake ran the model through ModelCache.LoadModel, so
         * _instantiateModelParams had already folded
         * FirstInstanceDescTransformWithInstance into ModelTransform before the file
         * was written - and the runtime folded it in a second time. On the character
         * rigs that matrix carries the fbx cm->m scaling, so the mesh rendered at
         * ~1/10000 size, i.e. invisibly, while bone-attached objects kept moving
         * correctly because they read the animation matrices instead.
         *
         * The rest of this comparison was thorough about geometry and skeleton and
         * simply did not look here. It does now.
         */
        _assertMatrixEqual(expected.ModelTransform, actual.ModelTransform, $"{what}: ModelTransform");
        _assertClose(expected.MaxDistance, actual.MaxDistance, $"{what}: MaxDistance");

        Assert.Equal(expected.Meshes.Count, actual.Meshes.Count);
        Assert.Equal(expected.MeshMaterials, actual.MeshMaterials);
        Assert.Equal(expected.Materials.Count, actual.Materials.Count);
        Assert.Equal(expected.ModelNodes.Count, actual.ModelNodes.Count);

        for (int i = 0; i < expected.ModelNodes.Count; ++i)
        {
            Assert.Equal(expected.ModelNodes[i]?.Name, actual.ModelNodes[i]?.Name);
        }

        for (int i = 0; i < expected.Meshes.Count; ++i)
        {
            _assertMeshesMatch(expected.Meshes[i], actual.Meshes[i], $"{what}/mesh[{i}]");
        }

        for (int i = 0; i < expected.Materials.Count; ++i)
        {
            var e = expected.Materials[i];
            var a = actual.Materials[i];
            Assert.Equal(e.Name, a.Name);
            Assert.Equal(e.Flags, a.Flags);
            Assert.Equal(e.AlbedoColor, a.AlbedoColor);
            Assert.Equal(e.EmissiveColor, a.EmissiveColor);
            Assert.Equal(e.EmissiveFactors, a.EmissiveFactors);
            Assert.Equal(e.Texture?.Source, a.Texture?.Source);
            Assert.Equal(e.Texture?.Key, a.Texture?.Key);
            Assert.Equal(e.EmissiveTexture?.Source, a.EmissiveTexture?.Source);
        }
    }


    private void _assertMeshesMatch(Mesh expected, Mesh actual, string what)
    {
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Vertices.Count, actual.Vertices.Count);
        Assert.Equal(expected.Indices, actual.Indices);

        for (int i = 0; i < expected.Vertices.Count; ++i)
        {
            _assertClose(expected.Vertices[i].X, actual.Vertices[i].X, $"{what} vertex[{i}].X");
            _assertClose(expected.Vertices[i].Y, actual.Vertices[i].Y, $"{what} vertex[{i}].Y");
            _assertClose(expected.Vertices[i].Z, actual.Vertices[i].Z, $"{what} vertex[{i}].Z");
        }

        Assert.Equal(expected.UVs.Count, actual.UVs.Count);
        for (int i = 0; i < expected.UVs.Count; ++i)
        {
            _assertClose(expected.UVs[i].X, actual.UVs[i].X, $"{what} uv[{i}].X");
            _assertClose(expected.UVs[i].Y, actual.UVs[i].Y, $"{what} uv[{i}].Y");
        }

        if (null == expected.Normals)
        {
            Assert.Null(actual.Normals);
        }
        else
        {
            Assert.NotNull(actual.Normals);
            Assert.Equal(expected.Normals.Count, actual.Normals!.Count);
            for (int i = 0; i < expected.Normals.Count; ++i)
            {
                _assertClose(expected.Normals[i].X, actual.Normals[i].X, $"{what} normal[{i}].X");
                _assertClose(expected.Normals[i].Y, actual.Normals[i].Y, $"{what} normal[{i}].Y");
                _assertClose(expected.Normals[i].Z, actual.Normals[i].Z, $"{what} normal[{i}].Z");
            }
        }

        if (null == expected.BoneIndices)
        {
            Assert.Null(actual.BoneIndices);
        }
        else
        {
            Assert.NotNull(actual.BoneIndices);
            Assert.Equal(expected.BoneIndices.Count, actual.BoneIndices!.Count);
            for (int i = 0; i < expected.BoneIndices.Count; ++i)
            {
                // exact: these are indices into AllBakedMatrices, not measurements
                Assert.Equal(expected.BoneIndices[i].B0, actual.BoneIndices[i].B0);
                Assert.Equal(expected.BoneIndices[i].B1, actual.BoneIndices[i].B1);
                Assert.Equal(expected.BoneIndices[i].B2, actual.BoneIndices[i].B2);
                Assert.Equal(expected.BoneIndices[i].B3, actual.BoneIndices[i].B3);
            }
        }

        if (null == expected.BoneWeights)
        {
            Assert.Null(actual.BoneWeights);
        }
        else
        {
            Assert.NotNull(actual.BoneWeights);
            Assert.Equal(expected.BoneWeights.Count, actual.BoneWeights!.Count);
            for (int i = 0; i < expected.BoneWeights.Count; ++i)
            {
                _assertClose(expected.BoneWeights[i].X, actual.BoneWeights[i].X, $"{what} weight[{i}].X");
                _assertClose(expected.BoneWeights[i].Y, actual.BoneWeights[i].Y, $"{what} weight[{i}].Y");
                _assertClose(expected.BoneWeights[i].Z, actual.BoneWeights[i].Z, $"{what} weight[{i}].Z");
                _assertClose(expected.BoneWeights[i].W, actual.BoneWeights[i].W, $"{what} weight[{i}].W");
            }
        }
    }


    private void _assertDerivedTransformsMatch(Model expected, Model actual, string what)
    {
        Assert.Equal(expected.IsHierarchical, actual.IsHierarchical);
        Assert.Equal(expected.FirstInstanceDescNode?.Name, actual.FirstInstanceDescNode?.Name);
        _assertMatrixEqual(
            expected.FirstInstanceDescTransformWithInstance,
            actual.FirstInstanceDescTransformWithInstance,
            $"{what}: FirstInstanceDescTransformWithInstance");
        _assertMatrixEqual(
            expected.FirstInstanceDescTransformWoInstance,
            actual.FirstInstanceDescTransformWoInstance,
            $"{what}: FirstInstanceDescTransformWoInstance");
    }


    private static void _assertMatrixEqual(System.Numerics.Matrix4x4 e, System.Numerics.Matrix4x4 a, string what)
    {
        _assertClose(e.M11, a.M11, what); _assertClose(e.M12, a.M12, what);
        _assertClose(e.M13, a.M13, what); _assertClose(e.M14, a.M14, what);
        _assertClose(e.M21, a.M21, what); _assertClose(e.M22, a.M22, what);
        _assertClose(e.M23, a.M23, what); _assertClose(e.M24, a.M24, what);
        _assertClose(e.M31, a.M31, what); _assertClose(e.M32, a.M32, what);
        _assertClose(e.M33, a.M33, what); _assertClose(e.M34, a.M34, what);
        _assertClose(e.M41, a.M41, what); _assertClose(e.M42, a.M42, what);
        _assertClose(e.M43, a.M43, what); _assertClose(e.M44, a.M44, what);
    }


    private static void _assertClose(float expected, float actual, string what)
    {
        if (float.IsNaN(expected) && float.IsNaN(actual))
        {
            return;
        }

        Assert.True(Math.Abs(expected - actual) <= Tolerance,
            $"{what}: expected {expected}, got {actual} (tolerance {Tolerance}).");
    }
}
