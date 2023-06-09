﻿using System.Numerics;
using System.Collections.Generic;
using System.Security.Cryptography;
using engine.joyce;


namespace engine.joyce;

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

    public InstanceDesc()
    {
        ModelTransform = Matrix4x4.Identity;
        Meshes = new List<Mesh>();
        MeshMaterials = new List<int>();
        Materials = new List<Material>();
    }

    public InstanceDesc TransformedCopy(in Matrix4x4 m)
    {
        InstanceDesc id = new InstanceDesc(Meshes, MeshMaterials, Materials);
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
    
