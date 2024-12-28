
using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace engine.joyce.components;

[engine.IsPersistable]
public struct Instance3
{
    public InstanceDesc InstanceDesc { get; set; }

    public override string ToString()
    {
        return $"{{ instanceDesc: {InstanceDesc.ToString()} }}";
    }

    /**
     * Construct a new instance3.
     * Caution: This uses the lists from the description.
     */
    public Instance3(in engine.joyce.InstanceDesc instanceDesc)
    {
        InstanceDesc = instanceDesc;
    }
}
