using System;
using System.Numerics;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using engine.geom;
using static engine.Logger;


namespace engine.joyce;


/**
 * Describe one specific instance of a 3d object (aka Instance3 components)
 * Note that this is only a descriptor, not a lifetime object.
 */
public class InstanceDesc
{
    private Matrix4x4 _m = Matrix4x4.Identity;

    [JsonIgnore]
    public Matrix4x4 ModelTransform
    {
        get => _m;
        set
        {
            if (_m != value)
            {
                _haveAABBTransformed = false;
                _m = value;
            }
        }
    }

    public float MaxDistance = 200f;
    
    private IList<engine.joyce.Mesh> _meshes;
    public ReadOnlyCollection<Mesh> Meshes;
    
    private IList<int> _meshMaterials;
    public ReadOnlyCollection<int> MeshMaterials;
    
    private IList<engine.joyce.Material> _materials;
    public ReadOnlyCollection<Material> Materials;
    
    /**
     * In lack of a dedicated Animation data structure (on the level of material),
     * we abuse the modelNode to reference the corresponding model, per mesh by index.
     */
    private IList<engine.joyce.ModelNode> _modelNodes;
    public ReadOnlyCollection<ModelNode> ModelNodes;
    
    
    private bool _haveCenter = false;
    private bool _haveAABBMerged = true;
    private AABB _aabbMerged;
    private bool _haveAABBTransformed = false;
    private AABB _aabbTransformed;

    
    private Vector3 _vCenter;


    [JsonIgnore]
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
    
    
    [JsonIgnore]
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

    [JsonIgnore]
    public AABB AABBTransformed
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
        if (null != Meshes)
        {
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


    private void _setup()
    {
        _meshes = new List<Mesh>();
        Meshes = new ReadOnlyCollection<Mesh>(_meshes);
        _meshMaterials = new List<int>();
        MeshMaterials = new ReadOnlyCollection<int>(_meshMaterials);
        _materials = new List<Material>();
        Materials = new ReadOnlyCollection<Material>(_materials);
        _modelNodes = new List<ModelNode>();
        ModelNodes = new ReadOnlyCollection<ModelNode>(_modelNodes);
    }


    public InstanceDesc()
    {
        _m = Matrix4x4.Identity;
        MaxDistance = 200f;
    }

    
    /**
     * Create a new instance desc from the matmesh given.
     */
    public static InstanceDesc CreateFromMatMesh(MatMesh matmesh, float maxDistance)
    {
        InstanceDesc id = new();
        id._setup();
        int materialIndex = 0;
        
        foreach (var kvp in matmesh.Tree)
        {
            id._materials.Add(kvp.Key);
            foreach (var (me,mn) in kvp.Value)
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
                id._modelNodes.Add(mn);
            }

            ++materialIndex;
        }

        id._haveAABBMerged = true;
        id._haveAABBTransformed = false;

        id.MaxDistance = maxDistance;
        
        return id;
    }
    
    
    public InstanceDesc TransformedCopy(in Matrix4x4 m)
    {
        InstanceDesc id = new InstanceDesc(Meshes, MeshMaterials, Materials, ModelNodes, this.MaxDistance);
        id._m = _m * m;
        return id;
    }
    
    
    /**
     * Compute a model adjustment matrix based on the model info
     * and the InstantiateModelParams. 
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
                        : this.AABBTransformed.Center.X)
                : 0f,
            (p.GeomFlags & InstantiateModelParams.CENTER_Y) != 0
                ? (
                    (p.GeomFlags & InstantiateModelParams.CENTER_Y_POINTS) != 0
                        ? this.Center.Y
                        : this.AABBTransformed.Center.Y)
                : 0f,
            (p.GeomFlags & InstantiateModelParams.CENTER_Z) != 0
                ? (
                    (p.GeomFlags & InstantiateModelParams.CENTER_Z_POINTS) != 0
                        ? this.Center.Z
                        : this.AABBTransformed.Center.Z)
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

    
    private void _ctor(         
        in IList<engine.joyce.Mesh> meshes,
        in IList<int> meshMaterials,
        in IList<engine.joyce.Material> materials,
        in IList<engine.joyce.ModelNode> modelNodes,
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
        _modelNodes = modelNodes;
        ModelNodes = new ReadOnlyCollection<ModelNode>(_modelNodes);
        _haveAABBMerged = false;
        _haveAABBTransformed = false;
    }

    private static List<ModelNode> _emptyModelNodeList = new();


    #if false
    public InstanceDesc(
        in IList<engine.joyce.Mesh> meshes,
        in IList<int> meshMaterials,
        in IList<engine.joyce.Material> materials,
        float maxDistance
    ) => _ctor(meshes, meshMaterials, materials, _emptyModelNodeList, maxDistance);
    #endif

    
    public InstanceDesc(
        in IList<engine.joyce.Mesh> meshes,
        in IList<int> meshMaterials,
        in IList<engine.joyce.Material> materials,
        in IList<engine.joyce.ModelNode> modelNodes,
        float maxDistance
    ) => _ctor(meshes, meshMaterials, materials, modelNodes, maxDistance);
}
    
