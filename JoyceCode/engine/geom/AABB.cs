using System;
using System.Numerics;

namespace engine.geom;

public struct AABB
{
    public Vector3 AA;
    public Vector3 BB;

    
    public Vector3 Center
    {
        get => (AA + BB) / 2f;
    }
    

    public float Radius
    {
        get => (BB - AA).Length() / 2f;
    }
    
    
    public void Offset(in Vector3 off)
    {
        AA += off;
        BB += off;
    }
    

    public void RotateY180()
    {
        /*
         * Rotating 180 degree on the Y axis means
         */
        Vector3 newAA = new(-BB.X, AA.Y, -BB.Z);
        Vector3 newBB = new(-AA.X, BB.Y, -AA.Z);
        AA = newAA;
        BB = newBB;
    }
    

    public override string? ToString()
    {
        return $"AABB {{ AA: {AA}, BB: {BB} }}";
    }
    
    
    public bool Intersects(in AABB o)
    {
        if (o.AA.X > BB.X || AA.X > o.BB.X || o.AA.Y > BB.Y || AA.Y > o.BB.Y || o.AA.Z > BB.Z || AA.Z > o.BB.Z)
        {
            return false;
        }

        return true;
    }
    
    
    public bool IntersectsXZ(in AABB o)
    {
        if (o.AA.X > BB.X || AA.X > o.BB.X || o.AA.Y > BB.Y || AA.Y > o.BB.Y || o.AA.Z > BB.Z || AA.Z > o.BB.Z)
        {
            return false;
        }

        return true;
    }


    public void Add(in Vector3 v)
    {
        AA.X = Single.Min(AA.X, v.X);
        AA.Y = Single.Min(AA.Y, v.Y);
        AA.Z = Single.Min(AA.Z, v.Z);
        BB.X = Single.Max(AA.X, v.X);
        BB.Y = Single.Max(AA.Y, v.Y);
        BB.Z = Single.Max(AA.Z, v.Z);
    }


    public AABB(in Vector3 aa, in Vector3 bb)
    {
        AA = aa;
        BB = bb;
    }

    
    public AABB(in Vector3 pos, float size)
    {
        Vector3 vS2 = new(size / 2f, size / 2f, size / 2f);
        AA = pos - vS2;
        BB = pos + vS2;
    }
}