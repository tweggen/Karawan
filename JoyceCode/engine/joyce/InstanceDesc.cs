using System;
using System.Numerics;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using BepuUtilities;
using engine.geom;
using engine.joyce;
using static engine.Logger;


namespace engine.joyce;

/**
 * Describe one specific instance of a 3d object (aka Instance3 components)
 * Note that this is only a descriptor, not a lifetime object.
 */
public class InstanceDesc
{
    private Matrix4x4 _m;

    public Matrix4x4 ModelTransform
    {
        get => _m;
        set
        {
            _haveAABBTransformed = false;
            _m = value;
        }
    }

    private IList<engine.joyce.Mesh> _meshes;
    public ReadOnlyCollection<Mesh> Meshes;
    
    private IList<int> _meshMaterials;
    public ReadOnlyCollection<int> MeshMaterials;
    
    private IList<engine.joyce.Material> _materials;
    public ReadOnlyCollection<Material> Materials;
    

    private bool _haveAABBMerged = true;
    private bool _haveAABBTransformed = false;
    
    
    private AABB _aabbMerged;
    public AABB AABBMerged
    {
        get
        {
            if (!_haveAABBMerged)
            {
                _computeAABBMerged();
                _haveAABBMerged = true;
                _haveAABBTransformed = false;
            }

            return _aabbMerged;
        }
        set
        {
            _aabbMerged = value;
            _haveAABBMerged = true;
            _haveAABBTransformed = false;
        }
    }

    private AABB _aabbTransformed;
    public AABB Aabb
    {
        get
        {
            if (!_haveAABBTransformed)
            {
                _computeAABBTransformed();
                _haveAABBTransformed = true;
            }

            return _aabbTransformed;
        }
    }


    private void _computeAABBMerged()
    {
        _aabbMerged.Reset();
        foreach (var mesh in Meshes)
        {
            _aabbMerged.Add(mesh.AABB);
        }
    }


    private void _computeAABBTransformed()
    {
        if (!_haveAABBMerged)
        {
            _computeAABBMerged();
            _haveAABBMerged = true;
        }
        _aabbTransformed = _aabbMerged;
        _aabbTransformed.Transform(_m);
        _haveAABBTransformed = true;
    }
    

    public void CheckIntegrity()
    {
        if (_meshes.Count != _meshMaterials.Count)
        {
            ErrorThrow(
                $"Internal mismatch: number of meshes and mesh materials don't match {Meshes.Count} != {MeshMaterials.Count}",
                (m) => new InvalidOperationException(m));
            return;
        }
        
    }

    
    public int FindMaterial(in Material material)
    {
        int nm = _materials.Count;
        int idx = -1;
            for (int i = 0; i < nm; ++i)
            {
                if (_materials[i] == material)
                {
                    idx = i;
                    break;
                }
            }

        if (-1 == idx)
        {
            idx = nm;
            _materials.Add(material);
            nm++;
        }

        return idx;
    }
    
    
    public void AddMesh(in Mesh mesh, int materialIndex)
    {
        CheckIntegrity();
        _meshes.Add(mesh);
        _aabbMerged.Add(mesh.AABB);
        _haveAABBMerged = true;
        _haveAABBTransformed = false;
        _meshMaterials.Add(materialIndex);
    }


    /**
     * Create a new instance desc from the matmesh given.
     */
    public static InstanceDesc CreateFromMatMesh(MatMesh matmesh)
    {
        InstanceDesc id = new();

        int materialIndex = 0;
        foreach (var kvp in matmesh.Tree)
        {
            id._materials.Add(kvp.Key);
            foreach (var me in kvp.Value)
            {
                id._meshes.Add(me);
                id._aabbMerged.Add(me.AABB);
                id._meshMaterials.Add(materialIndex);
            }

            ++materialIndex;
        }

        id._haveAABBMerged = true;
        id._haveAABBTransformed = false;

        return id;
    }
    
    
    private InstanceDesc()
    {
        _m = Matrix4x4.Identity;
        _meshes = new List<Mesh>();
        Meshes = new ReadOnlyCollection<Mesh>(_meshes);
        _meshMaterials = new List<int>();
        MeshMaterials = new ReadOnlyCollection<int>(_meshMaterials);
        _materials = new List<Material>();
        Materials = new ReadOnlyCollection<Material>(_materials);
    }

    
    public InstanceDesc TransformedCopy(in Matrix4x4 m)
    {
        InstanceDesc id = new InstanceDesc(Meshes, MeshMaterials, Materials);
        id._m = _m * m;
        return id;
    }

    
    public InstanceDesc(
        in IList<engine.joyce.Mesh> meshes,
        in IList<int> meshMaterials,
        in IList<engine.joyce.Material> materials
    )
    {
        _m = Matrix4x4.Identity;
        _meshes = meshes;
        Meshes = new ReadOnlyCollection<Mesh>(_meshes);
        _meshMaterials = meshMaterials;
        MeshMaterials = new ReadOnlyCollection<int>(_meshMaterials);
        _materials = materials;
        Materials = new ReadOnlyCollection<Material>(_materials);
        _haveAABBMerged = false;
        _haveAABBTransformed = false;
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
        MatMesh mm = new();
        foreach (var instanceDesc in listInstances)
        {
            mm.Add(instanceDesc);
        }
        MatMesh mmmerged = MatMesh.CreateMerged(mm);
        return CreateFromMatMesh(mmmerged);
    }
}
    
