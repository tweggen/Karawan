using System.Collections.Generic;

namespace Splash.components;

struct PfInstance
{
    public IList<engine.joyce.Mesh> Meshes;
    public IList<engine.joyce.Material> Materials;
    public IList<AMeshEntry> AMeshEntries;
    public IList<AMaterialEntry> AMaterialEntries;


    public PfInstance(in IList<engine.joyce.Mesh> meshes, in IList<engine.joyce.Material> materials)
    {
        Meshes = meshes;
        Materials = materials;
        AMeshEntries = new List<AMeshEntry>();
        AMaterialEntries = new List<AMaterialEntry>();
    }
}