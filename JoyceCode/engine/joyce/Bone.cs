using System.Collections.Generic;
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
 * However, we allow a maximum of 4 bones to influence a given mesh.
 *
 * While loading meshes, we build up this data structure to gather
 * the weight information per mesh.
 */
public class BoneMesh
{
    public VertexWeight[] VertexWeights;
    private int _nextVertexWeight = 0;
    private float _totalWeight = 0f;
    public Bone Bone;


    public float GetAverageWeight()
    {
        int l = VertexWeights.Length;
        if (l > 0)
        {
            return _totalWeight / l; 
        }
        else
        {
            return 0f;
        }
    }
    
    
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
    public Matrix4x4 InverseMatrix;
    public Dictionary<Mesh, BoneMesh>? _mapBoneMeshes = null;
    public uint Index;

    public BoneMesh FindBoneMesh(Mesh jMesh)
    {
        BoneMesh boneMesh;
        if (null == _mapBoneMeshes)
        {
            _mapBoneMeshes = new();
            boneMesh = new(this, (uint) jMesh.Vertices.Count);
            _mapBoneMeshes.Add(jMesh, boneMesh);
        }
        else
        {
            if (!_mapBoneMeshes.TryGetValue(jMesh, out boneMesh))
            {
                boneMesh = new(this, (uint) jMesh.Vertices.Count);
                _mapBoneMeshes.Add(jMesh, boneMesh);
            }
        }

        return boneMesh;
    }
    

    public void AddWeight(engine.joyce.Mesh jMesh, uint vertexIndex, float weight)
    {
        FindBoneMesh(jMesh).SetVertexWeight(vertexIndex, weight);
    }
}


