using System.Collections.Generic;
using System.Numerics;

namespace engine.joyce;

public struct VertexWeight
{
    public float Weight;
}

public class BoneMesh
{
    public VertexWeight[] VertexWeights;
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
    }


    public void SetVertexWeight(uint vertexIndex, float weight)
    {
        if (vertexIndex < VertexWeights.Length)
        {
            VertexWeights[vertexIndex].Weight = weight;
            _totalWeight += weight;
        }
    }
    
}

public class Bone
{
    public Matrix4x4 InverseMatrix;
    public Dictionary<Mesh, BoneMesh>? _mapBoneMeshes = null;
    public uint Index;

    public BoneMesh FindBoneMesh(Mesh jMesh)
    {
        BoneMesh boneMesh;
        if (null == _mapBoneMeshes)
        {
            _mapBoneMeshes = new();
            boneMesh = new(jMesh);
            _mapBoneMeshes.Add(jMesh, boneMesh);
        }
        else
        {
            if (!_mapBoneMeshes.TryGetValue(jMesh, out boneMesh))
            {
                boneMesh = new(jMesh);
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


