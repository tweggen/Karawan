using System;
using System.Numerics;

namespace Splash;

public class AMeshParams : IEquatable<AMeshParams>
{
    public engine.joyce.Mesh JMesh;
    public Vector2 UVOffset;
    public Vector2 UVScale;

    public bool Equals(AMeshParams other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Equals(JMesh, other.JMesh) && UVOffset.Equals(other.UVOffset) && UVScale.Equals(other.UVScale);
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((AMeshParams)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(JMesh, UVOffset, UVScale);
    }
}


public abstract class AMeshEntry : IDisposable
{
    public readonly AMeshParams Params;

    public abstract bool IsUploaded();

    public abstract bool IsFilled();
    
    public abstract void Dispose();

    protected AMeshEntry(in AMeshParams p)
    {
        Params = p;
    }
}