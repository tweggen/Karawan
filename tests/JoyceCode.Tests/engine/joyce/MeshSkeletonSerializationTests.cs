using System.Collections.Generic;
using System.Numerics;
using engine.joyce;
using MessagePack;
using Xunit;

namespace JoyceCode.Tests.engine.joyce;

/**
 * WP-4.1 spike: prove Mesh and Skeleton survive a MessagePack round trip before
 * the rest of the Model graph is annotated. These two were chosen by the plan
 * because between them they carry every hazard the rest of the graph repeats:
 * a value type of ours (Int4), lazily derived state (AABB, WriteIndex*),
 * process-local identity (IdHolder), and two collections sharing one set of
 * objects (Skeleton's list and map).
 */
public class MeshSkeletonSerializationTests
{
    private static readonly MessagePackSerializerOptions _options =
        MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);

    private static T RoundTrip<T>(T value)
    {
        var bytes = MessagePackSerializer.Serialize(value, _options);
        return MessagePackSerializer.Deserialize<T>(bytes, _options);
    }


    private static Mesh CreateSkinnedMesh()
    {
        var m = new Mesh(
            "test mesh",
            new List<Vector3> { new(0f, 0f, 0f), new(1f, 0f, 0f), new(0f, 1f, 0f) },
            new List<uint> { 0, 1, 2 },
            new List<Vector2> { new(0f, 0f), new(1f, 0f), new(0f, 1f) },
            new List<Vector3> { new(0f, 0f, 1f), new(0f, 0f, 1f), new(0f, 0f, 1f) });

        m.BoneIndices = new List<Int4>
        {
            new() { B0 = 0, B1 = 1, B2 = 0, B3 = 0 },
            new() { B0 = 1, B1 = 2, B2 = 0, B3 = 0 },
            new() { B0 = 2, B1 = 0, B2 = 0, B3 = 0 },
        };
        m.BoneWeights = new List<Vector4>
        {
            new(1f, 0f, 0f, 0f),
            new(0.5f, 0.5f, 0f, 0f),
            new(0.25f, 0.75f, 0f, 0f),
        };
        m.UploadImmediately = true;

        return m;
    }


    [Fact]
    public void MeshGeometryRoundTrips()
    {
        var m = CreateSkinnedMesh();
        var o = RoundTrip(m);

        Assert.Equal(m.Name, o.Name);
        Assert.Equal(m.Vertices, o.Vertices);
        Assert.Equal(m.Indices, o.Indices);
        Assert.Equal(m.UVs, o.UVs);
        Assert.Equal(m.Normals, o.Normals);
        Assert.Equal(m.UploadImmediately, o.UploadImmediately);
    }


    [Fact]
    public void MeshBoneWeightsRoundTrip()
    {
        var m = CreateSkinnedMesh();
        var o = RoundTrip(m);

        Assert.NotNull(o.BoneIndices);
        Assert.NotNull(o.BoneWeights);
        Assert.Equal(m.BoneIndices.Count, o.BoneIndices.Count);
        for (int i = 0; i < m.BoneIndices.Count; ++i)
        {
            Assert.Equal(m.BoneIndices[i].B0, o.BoneIndices[i].B0);
            Assert.Equal(m.BoneIndices[i].B1, o.BoneIndices[i].B1);
            Assert.Equal(m.BoneIndices[i].B2, o.BoneIndices[i].B2);
            Assert.Equal(m.BoneIndices[i].B3, o.BoneIndices[i].B3);
        }

        Assert.Equal(m.BoneWeights, o.BoneWeights);
    }


    [Fact]
    public void MeshWithoutNormalsOrBonesRoundTrips()
    {
        var m = new Mesh(
            "unskinned",
            new List<Vector3> { new(1f, 2f, 3f) },
            new List<uint> { 0 },
            new List<Vector2> { new(0.5f, 0.5f) });

        var o = RoundTrip(m);

        Assert.Equal(m.Vertices, o.Vertices);
        Assert.Null(o.Normals);
        Assert.Null(o.BoneIndices);
        Assert.Null(o.BoneWeights);
    }


    /**
     * The AABB is not in the file. Assert it comes back correct anyway, because
     * "recompute rather than persist" is only sound if the recomputation actually
     * happens on a deserialised mesh.
     */
    [Fact]
    public void MeshAabbIsRecomputedAfterLoad()
    {
        var m = CreateSkinnedMesh();
        var expected = m.AABB;
        var o = RoundTrip(m);

        Assert.Equal(expected.AA, o.AABB.AA);
        Assert.Equal(expected.BB, o.AABB.BB);
    }


    /**
     * The write cursors are how a mesh keeps being appendable after load. The
     * public constructors set them from the collection counts; a deserialised mesh
     * must end up in the same state, or the next p()/Idx() call inserts at 0 and
     * silently corrupts the geometry.
     */
    [Fact]
    public void MeshWriteCursorsAreRestored()
    {
        var m = CreateSkinnedMesh();
        var o = RoundTrip(m);

        Assert.Equal(o.Vertices.Count, o.WriteIndexVertices);
        Assert.Equal(o.Indices.Count, o.WriteIndexIndices);
        Assert.Equal(o.UVs.Count, o.WriteIndexUVs);
        Assert.Equal(o.Normals.Count, o.WriteIndexNormals);
    }


    /**
     * Identity must be regenerated, not restored. Two meshes loaded from the same
     * bytes are two different meshes; if they shared an id they would collide as
     * dictionary keys in MatMesh, and if the id defaulted to 0 EVERY loaded mesh
     * would compare equal to every other.
     */
    [Fact]
    public void DeserialisedMeshesGetDistinctIdentities()
    {
        var m = CreateSkinnedMesh();
        var bytes = MessagePackSerializer.Serialize(m, _options);

        var a = MessagePackSerializer.Deserialize<Mesh>(bytes, _options);
        var b = MessagePackSerializer.Deserialize<Mesh>(bytes, _options);

        Assert.NotEqual(0, a.GetHashCode());
        Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
        Assert.False(a.Equals(b));
        Assert.NotEqual(0, a.CompareTo(b));

        var set = new SortedSet<Mesh> { a, b };
        Assert.Equal(2, set.Count);
    }


    private static Skeleton CreateSkeleton()
    {
        var s = new Skeleton();
        // Deliberately NOT in alphabetical order: the map is sorted by name, the
        // list is in index order, and AllBakedMatrices is indexed by the latter.
        s.FindBone("Root_M").Model2Bone = Matrix4x4.CreateTranslation(1f, 0f, 0f);
        s.FindBone("Elbow_L").Model2Bone = Matrix4x4.CreateTranslation(0f, 2f, 0f);
        s.FindBone("Ankle_R").Model2Bone = Matrix4x4.CreateTranslation(0f, 0f, 3f);
        return s;
    }


    [Fact]
    public void SkeletonBoneOrderAndIndicesRoundTrip()
    {
        var s = CreateSkeleton();
        var o = RoundTrip(s);

        Assert.Equal(s.NBones, o.NBones);
        Assert.Equal(s.ListBones.Count, o.ListBones.Count);
        for (int i = 0; i < s.ListBones.Count; ++i)
        {
            Assert.Equal(s.ListBones[i].Name, o.ListBones[i].Name);
            Assert.Equal(s.ListBones[i].Index, o.ListBones[i].Index);
            Assert.Equal(s.ListBones[i].Model2Bone, o.ListBones[i].Model2Bone);
            Assert.Equal(s.ListBones[i].Bone2Model, o.ListBones[i].Bone2Model);
            // AC-4.3: list position IS the bone index.
            Assert.Equal(i, o.ListBones[i].Index);
        }
    }


    /**
     * The reference-sharing assertion, and the whole reason the map is rebuilt
     * rather than serialised: a lookup by name must return the very same object
     * as the lookup by index, not a copy carrying equal values.
     */
    [Fact]
    public void SkeletonMapAndListShareTheSameBoneObjects()
    {
        var o = RoundTrip(CreateSkeleton());

        Assert.Equal(o.ListBones.Count, o.MapBones.Count);
        foreach (var bone in o.ListBones)
        {
            Assert.True(o.MapBones.TryGetValue(bone.Name, out var mapped));
            Assert.Same(bone, mapped);
        }
    }


    /**
     * A loaded skeleton must still be usable as a skeleton: FindBone has to
     * recognise the bones already in it and hand out the next free index for one
     * it does not know, rather than restart numbering and alias an existing bone.
     */
    [Fact]
    public void FindBoneOnALoadedSkeletonContinuesTheIndexSequence()
    {
        var o = RoundTrip(CreateSkeleton());

        Assert.Same(o.ListBones[0], o.FindBone("Root_M"));
        Assert.Equal(3, o.NBones);

        var fresh = o.FindBone("Wrist_L");
        Assert.Equal(3, fresh.Index);
        Assert.Equal(4, o.NBones);
        Assert.Equal(4, o.ListBones.Count);
    }


    [Fact]
    public void EmptySkeletonRoundTrips()
    {
        var o = RoundTrip(new Skeleton());

        Assert.Equal(0, o.NBones);
        Assert.Empty(o.ListBones);
        Assert.Empty(o.MapBones);
    }
}
