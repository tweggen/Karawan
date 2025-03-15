using System;

namespace Splash;


/**
 * Helper that is used in the material batch.
 * Defines the mergability of a material entry.
 * Two materials are equal in the sense of this, if they share the same shader
 * properties and the same texture.
 */
internal struct MaterialBatchKey : IEquatable<MaterialBatchKey>
{
    public AMaterialEntry AMaterialEntry;

    public bool Equals(MaterialBatchKey other)
    {
        return AMaterialEntry.JMaterial.IsMergableEqual(other.AMaterialEntry.JMaterial);
    }

    public override bool Equals(object obj)
    {
        return obj is MaterialBatchKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        return (AMaterialEntry != null ? AMaterialEntry.JMaterial.GetMergableHashCode() : 0);
    }

    public MaterialBatchKey(AMaterialEntry aMaterialEntry)
    {
        AMaterialEntry = aMaterialEntry;
    }
} 
