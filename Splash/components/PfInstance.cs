using System.Collections.Generic;
using System.Numerics;

namespace Splash.components;

public struct PfInstance
{
    public Matrix4x4 ModelTransform;
    public IList<engine.joyce.Mesh> Meshes;
    public IList<int> MeshMaterials;
    public IList<engine.joyce.Material> Materials;
    public IList<AMeshEntry> AMeshEntries;
    public IList<AMaterialEntry> AMaterialEntries;


    public PfInstance(
        in Matrix4x4 modelTransform,
        in IList<engine.joyce.Mesh> meshes,
        in IList<int> meshMaterials,
        in IList<engine.joyce.Material> materials)
    {
        ModelTransform = modelTransform;
        Meshes = meshes;
        MeshMaterials = meshMaterials;
        Materials = materials;
        AMeshEntries = new List<AMeshEntry>();
        AMaterialEntries = new List<AMaterialEntry>();
    }
}