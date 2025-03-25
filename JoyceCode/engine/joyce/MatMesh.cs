using System.Collections.Generic;
using System.Linq;
using static engine.Logger;

namespace engine.joyce;

/**
 * Structured representation of a collection of meshes with a set of
 * materials.
 *
 * Completely ignores mesh properties.
 */
public class MatMesh
{
    public Dictionary<Material, List<(Mesh,ModelNode?)>> Tree;


    public bool IsEmpty()
    {
        return Tree.Count == 0;
    }


    public void Add(Material material, Mesh mesh) => Add(material, mesh, null);
    
    
    public void Add(Material material, Mesh mesh, ModelNode modelNode)
    {
        List<(Mesh,ModelNode?)> meshlist;
        if (!Tree.TryGetValue(material, out meshlist))
        {
            meshlist = new List<(Mesh,ModelNode)>();
            Tree[material] = meshlist;
        }
        meshlist.Add((mesh,modelNode));
    }
    
    
    public void Add(InstanceDesc id)
    {
        int l = id.Meshes.Count;
        for (int i = 0; i < l; i++)
        {
            List<(Mesh, ModelNode)> meshlist;
            if (!Tree.TryGetValue(id.Materials[id.MeshMaterials[i]], out meshlist))
            {
                meshlist = new List<(Mesh, ModelNode)>();
                Tree[id.Materials[id.MeshMaterials[i]]] = meshlist;
            }


            if (id.ModelTransform.IsIdentity)
            {
                meshlist.Add((id.Meshes[i], id.ModelNodes[i]));
            }
            else
            {
                Mesh tm = Mesh.CreateFrom( new List<Mesh>(){ id.Meshes[i] } );
                tm.Transform(id.ModelTransform);
                meshlist.Add((tm,null));
            }
        }
    }


    /**
     * Optimize the material mesh tree by merging meshes of the same
     * material.
     */
    public static MatMesh CreateMerged(MatMesh sm)
    {
        MatMesh tm = new();
        
        foreach (var kvp in sm.Tree)
        {
            var list = kvp.Value;

            if (list.Count < 1) continue;

            var meshlist = list.Select(k => k.Item1).ToList();
            Mesh mergedMesh = Mesh.CreateFrom(meshlist);
            // Trace($"merged mesh {mergedMesh.Name} with {mergedMesh.Vertices.Count} vertices");
            List<(Mesh,ModelNode)> lm = new();
            lm.Add((mergedMesh, null));
            tm.Tree[kvp.Key] = lm;

        }

        return tm;
    }


    public MatMesh()
    {
        Tree = new();
    }


    public MatMesh(Material material, Mesh mesh)
    {
        Tree = new();
        Add(material, mesh);
    }
    
}