using System;
using System.Numerics;
using System.Collections.Generic;
using engine.joyce;
using static engine.Logger;


namespace engine.joyce;

public class MeshProperties
{
    static public uint BillboardTransform = 0x00000001;
    public uint MeshFlags;
    public float MinDistance;
    public float MaxDistance;
}

/**
 * Describe one specific instance of a 3d object (aka Instance3 components)
 * Note that this is only a descriptor, not a lifetime object.
 */
public class InstanceDesc
{
    public Matrix4x4 ModelTransform;
    public IList<engine.joyce.Mesh> Meshes;
    public IList<int> MeshMaterials;
    public IList<engine.joyce.Material> Materials;
    public IList<MeshProperties> MeshProperties;


    public void NeedMeshProperties()
    {
        if (null == MeshProperties)
        {
            MeshProperties = new List<MeshProperties>();
        }
    }


    public void CheckIntegrity()
    {
        if (Meshes.Count != MeshMaterials.Count)
        {
            ErrorThrow(
                $"Internal mismatch: number of meshes and mesh materials don't match {Meshes.Count} != {MeshMaterials.Count}",
                (m) => new InvalidOperationException(m));
            return;
        }
        
    }
    
    public void SetMeshProperties(int idx, MeshProperties newmp)
    {
        NeedMeshProperties();
        CheckIntegrity();
        while (MeshProperties.Count <= (idx + 1))
        {
            MeshProperties.Add(null);
        }

        MeshProperties[idx] = newmp;
    }


    public void AddMesh(in Mesh mesh, int materialIndex, MeshProperties meshProperties)
    {
        CheckIntegrity();
        Meshes.Add(mesh);
        MeshMaterials.Add(materialIndex);
        int idx = Meshes.Count-1;
        SetMeshProperties(idx, meshProperties);
    }
    
    
    public InstanceDesc()
    {
        ModelTransform = Matrix4x4.Identity;
        Meshes = new List<Mesh>();
        MeshMaterials = new List<int>();
        Materials = new List<Material>();
    }

    public InstanceDesc TransformedCopy(in Matrix4x4 m)
    {
        InstanceDesc id = new InstanceDesc(Meshes, MeshMaterials, Materials, MeshProperties);
        id.ModelTransform = ModelTransform * m;
        return id;
    }

    
    public InstanceDesc(
        in IList<engine.joyce.Mesh> meshes,
        in IList<int> meshMaterials,
        in IList<engine.joyce.Material> materials
    )
    {
        ModelTransform = Matrix4x4.Identity;
        Meshes = meshes;
        MeshMaterials = meshMaterials;
        Materials = materials;
        MeshProperties = null;
    }


    public InstanceDesc(
        in IList<engine.joyce.Mesh> meshes,
        in IList<int> meshMaterials,
        in IList<engine.joyce.Material> materials,
        in IList<MeshProperties> meshProperties
    )
    {
        ModelTransform = Matrix4x4.Identity;
        Meshes = meshes;
        MeshMaterials = meshMaterials;
        Materials = materials;
        MeshProperties = meshProperties;
    }


    /**
     * Create a new InstanceDesc, merged from the source listInstances as much
     * as possible.
     *
     * This will create one mesh per different material, adding every single
     * vertex from all of the source instances.
     *
     * (we do not merge vertex points)
     */
    public static InstanceDesc CreateMergedFrom(IList<InstanceDesc> listInstances)
    {
        /*
         * this is our mesh accumulator, gathering the meshes per material.
         */
        SortedDictionary<Material, List<MergeMeshEntry>> mapListMeshes = new();
        
        /*
         * Group all meshes by materials.
         */
        foreach (var instanceDesc in listInstances)
        {
            for (int im = 0; im < instanceDesc.Meshes.Count; ++im)
            {
                Material jMaterial = instanceDesc.Materials[instanceDesc.MeshMaterials[im]];
                List<MergeMeshEntry> listMMEs;
                if (!mapListMeshes.TryGetValue(jMaterial, out listMMEs))
                {
                    listMMEs = new List<MergeMeshEntry>();
                    mapListMeshes.Add(jMaterial, listMMEs);
                }

                listMMEs.Add(new MergeMeshEntry()
                {
                    Transform = instanceDesc.ModelTransform, 
                    Mesh = instanceDesc.Meshes[im]
                });
            }
        }

        Mesh[] arrMeshes = new Mesh[mapListMeshes.Count];
        int[] arrMeshMaterials = new int[mapListMeshes.Count];
        Material[] arrMaterials = new Material[mapListMeshes.Count];

        /*
         * Merge the source meshes, emitting them into the new instance.
         */
        int materialIndex = 0;
        foreach (var kvp in mapListMeshes)
        {
            arrMeshes[materialIndex] = Mesh.CreateFrom(kvp.Value);
            arrMeshMaterials[materialIndex] = materialIndex;
            arrMaterials[materialIndex] = kvp.Key;
            ++materialIndex;
        }

        return new InstanceDesc(arrMeshes, arrMeshMaterials, arrMaterials);
    }

}
    
