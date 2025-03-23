using System.Collections.Generic;
using System.Numerics;
using engine.joyce;

namespace Splash.components;

public struct PfInstance
{
    //public Matrix4x4 ModelTransform;
    public IList<AMeshEntry> AMeshEntries;
    public IList<AMaterialEntry> AMaterialEntries;
    public AAnimationsEntry AAnimationsEntry;
    public InstanceDesc InstanceDesc;


    public PfInstance(
        InstanceDesc instanceDesc/*,
        in Matrix4x4 modelTransform*/)
    {
        //ModelTransform = modelTransform;
        AMeshEntries = new List<AMeshEntry>();
        AMaterialEntries = new List<AMaterialEntry>();
        InstanceDesc = instanceDesc;
    }
}