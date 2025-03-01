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


