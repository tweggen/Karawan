using System.Collections.Generic;
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
    public Dictionary<Material, List<Mesh>> Tree;


    public bool IsEmpty()
    {
        return Tree.Count == 0;
    }
    
    
    public void Add(Material material, Mesh mesh)
    {
        List<Mesh> meshlist;
        if (!Tree.TryGetValue(material, out meshlist))
        {
            meshlist = new List<Mesh>();
            Tree[material] = meshlist;
        }
        meshlist.Add(mesh);
    }
    
    
    public void Add(InstanceDesc id)
    {
        int l = id.Meshes.Count;
        for (int i = 0; i < l; i++)
        {
            List<Mesh> meshlist;
            if (!Tree.TryGetValue(id.Materials[id.MeshMaterials[i]], out meshlist))
            {
                meshlist = new List<Mesh>();
                Tree[id.Materials[id.MeshMaterials[i]]] = meshlist;
            }

#if false
            meshlist.Add(id.Meshes[i]);
#else
            if (id.ModelTransform.IsIdentity)
            {
                meshlist.Add(id.Meshes[i]);
            }
            else
            {
                Mesh tm = Mesh.CreateFrom( new List<Mesh>(){ id.Meshes[i] } );
                tm.Transform(id.ModelTransform);
                meshlist.Add(tm);
            }
#endif
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
            // TXWTODO: Why not 1?
            if (list.Count < 1) continue;

            Mesh mergedMesh = Mesh.CreateFrom(list);
            Trace($"merged mesh {mergedMesh.Name} with {mergedMesh.Vertices.Count} vertices");
            List<Mesh> lm = new();
            lm.Add(mergedMesh);
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