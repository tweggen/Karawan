using System.Collections.Generic;
using System.Numerics;
using engine.joyce;
using engine.joyce.mesh;
using engine.world;
using static engine.Logger;
using Trace = System.Diagnostics.Trace;

namespace builtin.loader;

class Cluster
{
    private Vector3 _sum;
    private int _nPoints;

    public Vector3 Center
    {
        get => _sum / (float)_nPoints;
    }


    public void Add(in Vector3 p)
    {
        _sum += p;
        _nPoints++;
    }
    
    
    public Cluster(in Vector3 p)
    {
        _sum = p;
        _nPoints = 1;
    }

}

/**
 * Given a model, this loader plugin searches for meshes of the model
 * marked as light, clusters them and adds light textures to them.
 *
 * This is a very inefficient algorithm, relying on clustering vertices
 * that are less than 30cm apart from each others.
 */
public class FindLights
{
    static private object _lock = new();
    

    private static void _addLightToModel(engine.Model model, Vector3 p)
    {
        InstanceDesc instanceDesc = model.InstanceDesc;
        int il = instanceDesc.FindMaterial(MaterialCache.Get("builtin.loader.materials.standardlight"));
        var m = Tools.CreatePlaneMesh("autolight", new Vector2(0.8f, 0.8f));
        m.Move(p);
        instanceDesc.AddMesh(m, il);
    }
    
        
    public static engine.Model Process(engine.Model model)
    {
        MaterialCache.Register("builtin.loader.materials.standardlight",
            name => new Material()
            {
                EmissiveColor = (bool) engine.Props.Get("debug.options.flatshading", false) != true
                    ? 0x00000000 : 0xccffffcc,
                EmissiveTexture = new engine.joyce.Texture("standardlight.png"),
                HasTransparency = true,
                IsBillboardTransform = true

            });
        
        InstanceDesc instanceDesc = model.InstanceDesc;
        if (null == instanceDesc.MeshMaterials || null == instanceDesc.Materials)
        {
            Trace("No mesh materials.");
            return model;
        }
        
        /*
         * First, collect all meshes of the lightmeshes material type
         * and add them to a list.
         *
         * We'll need to scan them for individual light later.
         */
        List<Mesh> listLightMeshes = new();
        int nm = instanceDesc.Meshes.Count;
        if (instanceDesc.MeshMaterials.Count < nm)
        {
            Trace($"Too little mesh materials: {instanceDesc.MeshMaterials.Count} < {nm}");
            return model;
        }
        for (int i = 0; i < nm; ++i)
        {
            Material material = instanceDesc.Materials[instanceDesc.MeshMaterials[i]];
            if (material.Name == "standardlight")
            {
                listLightMeshes.Add(instanceDesc.Meshes[i]);
            }
        }

        /*
         * This probably couldn't be less efficient
         */
        List<Cluster> listLights = new();

        /*
         * Nothing emitting light may have things further apart than 40cm.
         */
        float maxDistance = 0.3f;
        var closestCluster = (in Vector3 p, out Cluster foundC) =>
        {
            float maxDistance2 = maxDistance * maxDistance;
            foreach (Cluster c in listLights)
            {
                if (maxDistance2 >= (c.Center - p).LengthSquared())
                {
                    foundC = c;
                    return true;
                }
            }

            foundC = null;
            return false;
        };
        
        var addPoint = (Vector3 p) =>
        {
            if (closestCluster(p, out var c))
            {
                c.Add(p);
            }
            else
            {
                listLights.Add(new Cluster(p));
            }
        };
        
        foreach (Mesh mesh in listLightMeshes)
        {
            if (null != mesh.Vertices)
            {
                int l = mesh.Vertices.Count;
                for (int i = 0; i < l; i++)
                {
                    addPoint(mesh.Vertices[i]);
                }
            }
        }

        
        /*
         * Now, for each of the lights, we .... print them.
         */
        foreach (var c in listLights)
        {
            _addLightToModel(model, c.Center);
        }
        
        return model;
    }
}