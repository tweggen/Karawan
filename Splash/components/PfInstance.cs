using System.Collections.Generic;
using System.Collections.Immutable;
using System.Numerics;
using engine.joyce;

namespace Splash.components;

public struct PfInstance
{
    //public Matrix4x4 ModelTransform;
    public readonly ImmutableList<AMeshEntry> AMeshEntries;
    public readonly ImmutableList<AMaterialEntry> AMaterialEntries;
    public readonly ImmutableList<AAnimationsEntry> AAnimationsEntries;
    public InstanceDesc InstanceDesc;


    public PfInstance(
        InstanceDesc instanceDesc,
        ImmutableList<AMeshEntry> aMeshEntries,
        ImmutableList<AMaterialEntry> aMaterialEntries,
        ImmutableList<AAnimationsEntry> aAnimationsEntries)
    {
        InstanceDesc = instanceDesc;
        AMeshEntries = aMeshEntries;
        AMaterialEntries = aMaterialEntries;
        AAnimationsEntries = aAnimationsEntries;
    }
}