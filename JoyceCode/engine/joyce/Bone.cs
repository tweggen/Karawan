using System.Numerics;

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

public class Bone
{
    /**
     * The name of the bone. This corresponds with the name of the node it shall transform.
     */
    public string Name;
    
    /**
     * This matrix transforms from model space to bone space.
     * As such, it shall be the first part of any bone transformation.
     */
    public Matrix4x4 Model2Bone = Matrix4x4.Identity;

    public Matrix4x4 Bone2Model = Matrix4x4.Identity;
    
    public int Index;
}


