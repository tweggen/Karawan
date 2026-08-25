using System.Collections.Generic;
using System.Numerics;
using engine.joyce;
using engine.joyce.components;
using MessagePack;
using Xunit;

namespace JoyceCode.Tests.engine.joyce;

/**
 * WP-4.1: the whole Model graph through a MessagePack round trip.
 *
 * What these tests are really about is the graph's cycles. Nothing here would
 * fail loudly if the reassembly were wrong - a model with null Parent links still
 * renders, it just renders in the wrong place - so each back-reference is asserted
 * by object IDENTITY (Assert.Same) rather than by value. A copy carrying equal
 * values is exactly the failure mode MessagePack's lack of reference tracking
 * produces, and value equality would not see it.
 */
public class ModelGraphSerializationTests
{
    private static readonly MessagePackSerializerOptions _options =
        MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);


    private static Mesh CreateMesh(string name, float x)
    {
        return new Mesh(
            name,
            new List<Vector3> { new(x, 0f, 0f), new(x + 1f, 0f, 0f), new(x, 1f, 0f) },
            new List<uint> { 0, 1, 2 },
            new List<Vector2> { new(0f, 0f), new(1f, 0f), new(0f, 1f) },
            new List<Vector3> { new(0f, 0f, 1f), new(0f, 0f, 1f), new(0f, 0f, 1f) });
    }


    /**
     * A model shaped like the ones the FBX loader produces: a root, a child
     * carrying the geometry, a grandchild carrying none, a skeleton, and an
     * instance desc whose ModelNodes point back INTO the tree at two different
     * nodes - which is the cycle.
     */
    private static Model CreateModel()
    {
        var model = new Model
        {
            Name = "test model",
            ModelUrl = "test.fbx",
            Scale = 1f,
        };

        var mnRoot = new ModelNode
        {
            Model = model,
            ModelNodeTree = model.ModelNodeTree,
            Parent = null,
            Name = "Root_M",
            Transform = new Transform3ToParent(true, 0xffff, Matrix4x4.CreateTranslation(0f, 1f, 0f)),
        };

        var mnGeometry = new ModelNode
        {
            Model = model,
            ModelNodeTree = model.ModelNodeTree,
            Parent = mnRoot,
            Name = "Body",
            Transform = new Transform3ToParent(true, 0xffff, Matrix4x4.CreateScale(2f)),
        };
        mnRoot.AddChild(mnGeometry);

        var mnLeaf = new ModelNode
        {
            Model = model,
            ModelNodeTree = model.ModelNodeTree,
            Parent = mnGeometry,
            Name = "Elbow_L",
            Transform = new Transform3ToParent(true, 0xffff, Matrix4x4.CreateRotationZ(0.25f)),
        };
        mnGeometry.AddChild(mnLeaf);

        var texture = new Texture("joyce://col00ff00ff") { FilteringMode = Texture.FilteringModes.Smooth };
        var material = new Material
        {
            Name = "skin",
            Texture = texture,
            AlbedoColor = 0xff112233,
            EmissiveColor = 0xff000000,
            HasTransparency = true,
        };
        var material2 = new Material { Name = "cloth", AlbedoColor = 0xffaabbcc };

        var id = new InstanceDesc(
            new List<Mesh> { CreateMesh("body", 0f), CreateMesh("head", 10f) },
            new List<int> { 0, 1 },
            new List<Material> { material, material2 },
            // deliberately two DIFFERENT nodes, and neither of them the owning node
            new List<ModelNode> { mnLeaf, mnRoot },
            400f);
        /*
         * Assigned rather than passed: InstanceDesc's constructor takes a
         * maxDistance and never assigns it, so the field initialiser (200f) wins.
         * Pre-existing and out of scope for WP-4.1 - noted here because the test
         * would otherwise be asserting the default and proving nothing.
         */
        id.MaxDistance = 400f;
        mnGeometry.InstanceDesc = id;

        var skeleton = model.FindSkeleton();
        skeleton.FindBone("Root_M").Model2Bone = Matrix4x4.CreateTranslation(1f, 0f, 0f);
        skeleton.FindBone("Elbow_L").Model2Bone = Matrix4x4.CreateTranslation(0f, 2f, 0f);

        model.ModelNodeTree.SetRootNode(mnRoot, skeleton);
        model.Polish(null);

        return model;
    }


    private static Model RoundTrip(Model model)
    {
        var bytes = MessagePackSerializer.Serialize(model, _options);
        return MessagePackSerializer.Deserialize<Model>(bytes, _options);
    }


    [Fact]
    public void ModelIdentityRoundTrips()
    {
        var o = RoundTrip(CreateModel());

        Assert.Equal("test model", o.Name);
        Assert.Equal("test.fbx", o.ModelUrl);
        Assert.Equal(1f, o.Scale);
    }


    [Fact]
    public void NodeTreeStructureAndTransformsRoundTrip()
    {
        var m = CreateModel();
        var o = RoundTrip(m);

        Assert.Equal("Root_M", o.ModelNodeTree.RootNode.Name);
        Assert.Single(o.ModelNodeTree.RootNode.Children);

        var body = o.ModelNodeTree.RootNode.Children[0];
        Assert.Equal("Body", body.Name);
        Assert.Single(body.Children);
        Assert.Equal("Elbow_L", body.Children[0].Name);
        Assert.Null(body.Children[0].Children);

        Assert.Equal(
            m.ModelNodeTree.RootNode.Transform.Matrix,
            o.ModelNodeTree.RootNode.Transform.Matrix);
        Assert.Equal(m.ModelNodeTree.MapNodes["Body"].Transform.Matrix, body.Transform.Matrix);
        Assert.True(body.Transform.IsVisible);
        Assert.Equal(0xffffu, body.Transform.CameraMask);
    }


    /**
     * The upward edges. None of these is in the file.
     */
    [Fact]
    public void BackReferencesAreRestored()
    {
        var o = RoundTrip(CreateModel());

        var root = o.ModelNodeTree.RootNode;
        var body = root.Children[0];
        var elbow = body.Children[0];

        Assert.Null(root.Parent);
        Assert.Same(root, body.Parent);
        Assert.Same(body, elbow.Parent);

        foreach (var mn in new[] { root, body, elbow })
        {
            Assert.Same(o, mn.Model);
            Assert.Same(o.ModelNodeTree, mn.ModelNodeTree);
        }
    }


    [Fact]
    public void MapNodesIndexesTheSameObjectsAsTheSpine()
    {
        var o = RoundTrip(CreateModel());

        var root = o.ModelNodeTree.RootNode;
        var body = root.Children[0];
        var elbow = body.Children[0];

        Assert.Equal(3, o.ModelNodeTree.MapNodes.Count);
        Assert.Same(root, o.ModelNodeTree.MapNodes["Root_M"]);
        Assert.Same(body, o.ModelNodeTree.MapNodes["Body"]);
        Assert.Same(elbow, o.ModelNodeTree.MapNodes["Elbow_L"]);
    }


    /**
     * The cycle proper: instance desc -> model node -> ... -> instance desc.
     * Persisted as names, and it must come back pointing at the tree's own nodes,
     * not at fresh copies of them.
     */
    [Fact]
    public void InstanceDescModelNodesResolveBackIntoTheTree()
    {
        var o = RoundTrip(CreateModel());

        var body = o.ModelNodeTree.RootNode.Children[0];
        var id = body.InstanceDesc;
        Assert.NotNull(id);

        Assert.Equal(2, id.ModelNodes.Count);
        Assert.Same(o.ModelNodeTree.MapNodes["Elbow_L"], id.ModelNodes[0]);
        Assert.Same(o.ModelNodeTree.RootNode, id.ModelNodes[1]);

        // and the count still lines up with the meshes it is indexed against
        Assert.Equal(id.Meshes.Count, id.ModelNodes.Count);
    }


    [Fact]
    public void InstanceDescGeometryAndMaterialsRoundTrip()
    {
        var m = CreateModel();
        var o = RoundTrip(m);

        var idOriginal = m.ModelNodeTree.MapNodes["Body"].InstanceDesc;
        var id = o.ModelNodeTree.MapNodes["Body"].InstanceDesc;

        Assert.Equal(2, id.Meshes.Count);
        Assert.Equal("body", id.Meshes[0].Name);
        Assert.Equal("head", id.Meshes[1].Name);
        Assert.Equal(idOriginal.Meshes[0].Vertices, id.Meshes[0].Vertices);
        Assert.Equal(idOriginal.Meshes[1].Vertices, id.Meshes[1].Vertices);

        Assert.Equal(new[] { 0, 1 }, id.MeshMaterials);
        Assert.Equal(400f, id.MaxDistance);
        Assert.Equal(idOriginal.ModelTransform, id.ModelTransform);

        Assert.Equal(2, id.Materials.Count);
        Assert.Equal("skin", id.Materials[0].Name);
        Assert.Equal(0xff112233u, id.Materials[0].AlbedoColor);
        Assert.True(id.Materials[0].HasTransparency);
        Assert.Equal("cloth", id.Materials[1].Name);
        Assert.False(id.Materials[1].HasTransparency);
    }


    [Fact]
    public void MaterialTextureRoundTripsAndRecomputesItsKey()
    {
        var m = CreateModel();
        var o = RoundTrip(m);

        var expected = m.ModelNodeTree.MapNodes["Body"].InstanceDesc.Materials[0].Texture;
        var texture = o.ModelNodeTree.MapNodes["Body"].InstanceDesc.Materials[0].Texture;

        Assert.NotNull(texture);
        Assert.Equal("joyce://col00ff00ff", texture.Source);
        Assert.Equal(Texture.FilteringModes.Smooth, texture.FilteringMode);
        Assert.Equal(expected.Key, texture.Key);
        Assert.Null(o.ModelNodeTree.MapNodes["Body"].InstanceDesc.Materials[1].Texture);
    }


    [Fact]
    public void SkeletonRoundTripsWithTheModel()
    {
        var o = RoundTrip(CreateModel());

        Assert.NotNull(o.Skeleton);
        Assert.Equal(2, o.Skeleton.NBones);
        Assert.Equal("Root_M", o.Skeleton.ListBones[0].Name);
        Assert.Equal(0, o.Skeleton.ListBones[0].Index);
        Assert.Equal("Elbow_L", o.Skeleton.ListBones[1].Name);
        Assert.Equal(1, o.Skeleton.ListBones[1].Index);
        Assert.Same(o.Skeleton.ListBones[1], o.Skeleton.MapBones["Elbow_L"]);
    }


    /**
     * The derived state is not in the file by design. Assert Polish reproduces it
     * exactly - that is the whole justification for leaving it out.
     *
     * This is also the sharpest test of the Parent links: ComputeGlobalTransform
     * walks upward, so if Rebind had left Parent null these matrices would come
     * back as the node's own local transform and quietly differ.
     */
    [Fact]
    public void PolishReproducesTheDerivedTransformsAfterLoad()
    {
        var m = CreateModel();
        var o = RoundTrip(m);

        // Untouched by the file, so still at their defaults before Polish.
        Assert.Null(o.FirstInstanceDescNode);

        o.Polish(null);

        Assert.NotNull(o.FirstInstanceDescNode);
        Assert.Equal(m.FirstInstanceDescNode.Name, o.FirstInstanceDescNode.Name);
        Assert.Same(o.ModelNodeTree.MapNodes["Body"], o.FirstInstanceDescNode);

        Assert.Equal(m.IsHierarchical, o.IsHierarchical);
        Assert.Equal(
            m.FirstInstanceDescTransformWithInstance,
            o.FirstInstanceDescTransformWithInstance);
        Assert.Equal(
            m.InverseFirstInstanceDescTransformWithInstance,
            o.InverseFirstInstanceDescTransformWithInstance);
        Assert.Equal(
            m.FirstInstanceDescTransformWoInstance,
            o.FirstInstanceDescTransformWoInstance);
        Assert.Equal(
            m.InverseFirstInstanceDescTransformWoInstance,
            o.InverseFirstInstanceDescTransformWoInstance);
    }


    /**
     * A deserialised model must be ready to adopt an ac-{hash} animation
     * collection. It carries none of its own, and UseBakedAnimationsFrom merges
     * INTO MapAnimations - a null map there is a null dereference at the first
     * animated model load, on the baked path only.
     */
    [Fact]
    public void DeserialisedModelIsReadyToAdoptBakedAnimations()
    {
        var o = RoundTrip(CreateModel());

        Assert.NotNull(o.AnimationCollection);
        Assert.NotNull(o.AnimationCollection.MapAnimations);
        Assert.Empty(o.AnimationCollection.MapAnimations);

        var baked = new ModelAnimationCollection
        {
            MapAnimations = new SortedDictionary<string, ModelAnimation>
            {
                ["Walk"] = new ModelAnimation { Name = "Walk", Index = 1, FirstFrame = 0, NFrames = 10 },
            },
            AllBakedMatrices = new Matrix4x4[10 * 2],
        };

        Assert.True(o.AnimationCollection.TestBakedAnimationsFrom(baked));
        o.AnimationCollection.UseBakedAnimationsFrom(baked);
        Assert.Single(o.AnimationCollection.MapAnimations);
        Assert.Equal(10u, o.AnimationCollection.MapAnimations["Walk"].NFrames);
    }


    [Fact]
    public void ModelWithoutSkeletonOrInstanceDescRoundTrips()
    {
        var model = new Model { Name = "bare", ModelUrl = "bare.fbx" };
        var mnRoot = new ModelNode
        {
            Model = model,
            ModelNodeTree = model.ModelNodeTree,
            Parent = null,
            Name = "only",
            Transform = new Transform3ToParent(true, 0xffff, Matrix4x4.Identity),
        };
        model.ModelNodeTree.SetRootNode(mnRoot, null);

        var o = RoundTrip(model);

        Assert.Equal("only", o.ModelNodeTree.RootNode.Name);
        Assert.Null(o.Skeleton);
        Assert.Null(o.ModelNodeTree.RootNode.InstanceDesc);
        Assert.Same(o, o.ModelNodeTree.RootNode.Model);
    }
}
