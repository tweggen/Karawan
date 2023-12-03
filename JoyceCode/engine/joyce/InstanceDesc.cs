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

    public float MaxDistance = 200f;
    
    private IList<engine.joyce.Mesh> _meshes;
    public ReadOnlyCollection<Mesh> Meshes;
    
    private IList<int> _meshMaterials;
    public ReadOnlyCollection<int> MeshMaterials;
    
    private IList<engine.joyce.Material> _materials;
    public ReadOnlyCollection<Material> Materials;

    private bool _haveCenter = false;
    private bool _haveAABBMerged = true;
    private bool _haveAABBTransformed = false;


    private Vector3 _vCenter;
    public Vector3 Center
    {
        get
        {
            if (!_haveCenter)
            {
                _computeCenter();
                _haveCenter = true;
            }

            return _vCenter;
        }
    }
    
    
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
    public AABB AABB
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


    private void _computeCenter()
    {
        _vCenter = Vector3.Zero;
        int n = 0;
        foreach (var jm in Meshes)
        {
            int nv = jm.Vertices.Count;
            for (int iv = 0; iv < nv; ++iv)
            {
                _vCenter += jm.Vertices[iv];
            }

            n += nv;
        }

        if (0 != n)
        {
            _vCenter /= n;
        }
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
    public static InstanceDesc CreateFromMatMesh(MatMesh matmesh, float maxDistance)
    {
        InstanceDesc id = new();

        int materialIndex = 0;
        foreach (var kvp in matmesh.Tree)
        {
            id._materials.Add(kvp.Key);
            foreach (var me in kvp.Value)
            {
                if (me.Vertices.Count > 65535)
                {
                    Error($"Too much vertices in mesh {me.Name}.");
                    continue;
                }
                if (me.Indices.Count > 65535)
                {
                    Error($"Too much indices in mesh {me.Name}.");
                    continue;
                }

                id._meshes.Add(me);
                id._aabbMerged.Add(me.AABB);
                id._meshMaterials.Add(materialIndex);
            }

            ++materialIndex;
        }

        id._haveAABBMerged = true;
        id._haveAABBTransformed = false;

        id.MaxDistance = maxDistance;
        
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
        MaxDistance = 200f;
    }

    
    public InstanceDesc TransformedCopy(in Matrix4x4 m)
    {
        InstanceDesc id = new InstanceDesc(Meshes, MeshMaterials, Materials, this.MaxDistance);
        id._m = _m * m;
        return id;
    }
    
    
    /**
     * Compute a model adjustment matrix based on the model info
     * and thge InstantiateModelParams
     */
    public void ComputeAdjustMatrix(InstantiateModelParams? p, ref Matrix4x4 m)
    {
        if (p == null)
        {
            return;
        }
        
        /*
         * Now, according to the instantiateModelParams, modify the data we loaded.
         */
        Vector3 vReCenter = new(
            (p.GeomFlags & InstantiateModelParams.CENTER_X) != 0
                ? (
                    (p.GeomFlags & InstantiateModelParams.CENTER_X_POINTS) != 0
                        ? this.Center.X
                        : this.AABB.Center.X)
                : 0f,
            (p.GeomFlags & InstantiateModelParams.CENTER_Y) != 0
                ? (
                    (p.GeomFlags & InstantiateModelParams.CENTER_Y_POINTS) != 0
                        ? this.Center.Y
                        : this.AABB.Center.Y)
                : 0f,
            (p.GeomFlags & InstantiateModelParams.CENTER_Z) != 0
                ? (
                    (p.GeomFlags & InstantiateModelParams.CENTER_Z_POINTS) != 0
                        ? this.Center.Z
                        : this.AABB.Center.Z)
                : 0f
        );

        if (vReCenter != Vector3.Zero)
        {
            m = m * Matrix4x4.CreateTranslation(-vReCenter);
        }

        int rotX = ((0 != (p.GeomFlags & InstantiateModelParams.ROTATE_X90)) ? 1 : 0) +
                   ((0 != (p.GeomFlags & InstantiateModelParams.ROTATE_X180)) ? 2 : 0);
        int rotY = ((0 != (p.GeomFlags & InstantiateModelParams.ROTATE_Y90)) ? 1 : 0) +
                   ((0 != (p.GeomFlags & InstantiateModelParams.ROTATE_Y180)) ? 2 : 0);
        int rotZ = ((0 != (p.GeomFlags & InstantiateModelParams.ROTATE_Z90)) ? 1 : 0) +
                   ((0 != (p.GeomFlags & InstantiateModelParams.ROTATE_Z180)) ? 2 : 0);

        if (0 != rotX)
        {
            m *= Matrix4x4.CreateRotationX(Single.Pi * rotX / 2f);
        }

        if (0 != rotY)
        {
            m *= Matrix4x4.CreateRotationY(Single.Pi * rotY / 2f);
        }

        if (0 != rotZ)
        {
            m *= Matrix4x4.CreateRotationZ(Single.Pi * rotZ / 2f);
        }
    }
        
    
    public InstanceDesc(
        in IList<engine.joyce.Mesh> meshes,
        in IList<int> meshMaterials,
        in IList<engine.joyce.Material> materials,
        float maxDistance
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
    public static InstanceDesc CreateMergedFrom(IList<InstanceDesc> listInstances, float maxDistance)
    {
        MatMesh mm = new();
        foreach (var instanceDesc in listInstances)
        {
            mm.Add(instanceDesc);
        }
        MatMesh mmmerged = MatMesh.CreateMerged(mm);
        return CreateFromMatMesh(mmmerged, maxDistance);
    }
}
    
