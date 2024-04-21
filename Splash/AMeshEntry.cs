using System;
using System.Numerics;

namespace Splash;

public class AMeshParams : IComparable<AMeshParams>
{
    public engine.joyce.Mesh JMesh;
    public Vector2 UVOffset;
    public Vector2 UVScale;
    public int CompareTo(AMeshParams other)
    {
        int res;
        res = JMesh.CompareTo(other.JMesh);
        if (res != 0) return res;
        res = UVOffset.X.CompareTo(other.UVOffset.X);
        if (res != 0) return res;
        res = UVOffset.Y.CompareTo(other.UVOffset.Y);
        if (res != 0) return res;
        res = UVScale.X.CompareTo(other.UVScale.X);
        if (res != 0) return res;
        res = UVScale.Y.CompareTo(other.UVScale.Y);
        if (res != 0) return res;
        return res;
    }
}


public abstract class AMeshEntry : IDisposable
{
    public readonly AMeshParams Params;

    public abstract bool IsUploaded();

    public abstract void Dispose();

    protected AMeshEntry(in AMeshParams p)
    {
        Params = p;
    }
}