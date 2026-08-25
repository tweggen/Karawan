using System.Numerics;
using MessagePack;

namespace engine.joyce;

public struct VertexWeight
{
    public float Weight;
    public uint VertexIndex;
}


/**
 * Carry the weight and vertex data of a given bone for a single mesh.
 * A bone may very well influence other meshes as well.
 * Also other bones may influence this mesh as well.
 *
 * However, we allow a maximum of 4 bones to influence a given vertex.
 *
 * While loading meshes, we build up this data structure to gather
 * the weight information per mesh.
 */
public class BoneMesh
{
    public VertexWeight[] VertexWeights;
    private int _nextVertexWeight = 0;
    public Bone Bone;


    public BoneMesh(engine.joyce.Bone bone, uint nVertices)
    {
        VertexWeights = new VertexWeight[nVertices];
        Bone = bone;
    }


    public void SetVertexWeight(uint vertexIndex, float weight)
    {
        if (_nextVertexWeight == VertexWeights.Length)
        {
            return;
        }
        VertexWeights[_nextVertexWeight++] = new VertexWeight() { Weight = weight, VertexIndex = vertexIndex };
    }
    
}

/**
 * One bone of a Skeleton.
 *
 * Persisted as part of the baked mo-{hash} model (WP-4.1). Index is serialised
 * rather than re-derived: it is what AllBakedMatrices is indexed by
 * (frame * NBones + Index), so the baked model and the baked ac-{hash}
 * animations must agree on it exactly. See AC-4.3.
 */
[MessagePackObject]
public class Bone
{
    /**
     * The name of the bone. This corresponds with the name of the node it shall transform.
     */
    [Key(0)]
    public string Name;

    /**
     * This matrix transforms from model space to bone space.
     * As such, it shall be the first part of any bone transformation.
     */
    [Key(1)]
    public Matrix4x4 Model2Bone = Matrix4x4.Identity;

    [Key(2)]
    public Matrix4x4 Bone2Model = Matrix4x4.Identity;

    [Key(3)]
    public int Index;
}


