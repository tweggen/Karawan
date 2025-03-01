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

    public BoneMesh(engine.joyce.Mesh mesh)
    {
        VertexWeights = new VertexWeight[mesh.Vertices.Count];
    }


    public void SetVertexWeight(uint vertexIndex, float weight)
    {
        if (vertexIndex < VertexWeights.Length)
        {
            VertexWeights[vertexIndex].Weight = weight;
        }
    }
    
}

public class Bone
{
    public Matrix4x4 InverseMatrix;
    public Dictionary<Mesh, BoneMesh>? _mapBoneMeshes = null;


    public void AddWeight(engine.joyce.Mesh mesh, uint vertexIndex, float weight)
    {
        BoneMesh boneMesh;
        if (null == _mapBoneMeshes)
        {
            _mapBoneMeshes = new();
            boneMesh = new(mesh);
            _mapBoneMeshes.Add(mesh, boneMesh);
        }
        else
        {
            if (!_mapBoneMeshes.TryGetValue(mesh, out boneMesh))
            {
                boneMesh = new(mesh);
                _mapBoneMeshes.Add(mesh, boneMesh);
            }
        }
        
        boneMesh.SetVertexWeight(vertexIndex, weight);
    }
}


