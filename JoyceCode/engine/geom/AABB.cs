using System;
using System.Numerics;
using System.Runtime.InteropServices.Marshalling;

namespace engine.geom;

public struct AABB
{
    static public AABB All = new(
        new Vector3(Single.MinValue, Single.MinValue, Single.MinValue),
        new Vector3(Single.MaxValue, Single.MaxValue, Single.MaxValue));
    public Vector3 AA;
    public Vector3 BB;

    
    public static bool operator ==(in AABB a, in AABB b)
    {
        return a.AA == b.AA && a.BB == b.BB;
    }
    
    public static bool operator !=(in AABB a, in AABB b)
    {
        return a.AA != b.AA || a.BB != b.BB;
    }
    
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

    public void Transform(in Matrix4x4 m)
    {
        if (!m.IsIdentity)
        {
            // bool ok = Matrix4x4.Decompose(m, out var scale, out var rot, out var pos);
            Vector3 newAA = new(m.M41, m.M42, m.M43);
            Vector3 newBB = new(m.M41, m.M42, m.M43);

            for (int i = 0; i < 3; ++i)
            {
                for (int j = 0; j < 3; ++j)
                {
                    float a, b;
                    a = m[i, j] * AA[j];
                    b = m[i, j] * BB[j];
                    newAA[i] += Single.Min(a, b);
                    newBB[i] += Single.Max(a, b);
                }
            }

            AA = newAA;
            BB = newBB;
        }
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
        if (o.AA.X > BB.X || AA.X > o.BB.X || o.AA.Z > BB.Z || AA.Z > o.BB.Z)
        {
            return false;
        }

        return true;
    }


    public void Extend(float f)
    {
        Vector3 fMore = new(f, f, f);
        AA -= fMore;
        BB += fMore;
    }


    public void Add(in Vector3 v)
    {
        AA.X = Single.Min(AA.X, v.X);
        AA.Y = Single.Min(AA.Y, v.Y);
        AA.Z = Single.Min(AA.Z, v.Z);
        BB.X = Single.Max(BB.X, v.X);
        BB.Y = Single.Max(BB.Y, v.Y);
        BB.Z = Single.Max(BB.Z, v.Z);
    }


    public void Add(in AABB o)
    {
        AA.X = Single.Min(AA.X, o.AA.X);
        AA.Y = Single.Min(AA.Y, o.AA.Y);
        AA.Z = Single.Min(AA.Z, o.AA.Z);
        BB.X = Single.Max(BB.X, o.BB.X);
        BB.Y = Single.Max(BB.Y, o.BB.Y);
        BB.Z = Single.Max(BB.Z, o.BB.Z);
    }


    /**
     * Compute the signed distance to the plane defined by plane
     */
    public float SignedDistance(in Plane plane)
    {
        /*
         * This is the point in direction of the plane. 
         */
        Vector3 vc = new(
            plane.Normal.X < 0f ? AA.X : BB.X,
            plane.Normal.Y < 0f ? AA.Y : BB.Y,
            plane.Normal.Z < 0f ? AA.Z : BB.Z
        );

        return Vector3.Dot(vc,plane.Normal) + plane.D;
    }


    public bool Contains(in Vector3 pos)
    {
        return
            pos.X >= AA.X && pos.X <= BB.X &&
            pos.Y >= AA.Y && pos.Y <= BB.Y &&
            pos.Z >= AA.Z && pos.Z <= BB.Z;
    }


    public bool TryIntersect(in AABB i, out AABB o)
    {
        /*
         * We take our aabb and gradually shrink it.
         */
        o = this;
        
        /*
         * First test the cases where we don't overlap at all.
         */
        if (false
            || (i.AA.X > o.BB.X)
            || (i.AA.Y > o.BB.Y)
            || (i.AA.Z > o.BB.Z)
            || (i.BB.X < o.AA.X)
            || (i.BB.Y < o.AA.Y)
            || (i.BB.Z < o.AA.Z)
            )
        {
            return false;
        }
        
        /*
         * Otherwise, we overlap.
         */
        if (i.AA.X > o.AA.X) o.AA.X = i.AA.X;
        if (i.AA.Y > o.AA.Y) o.AA.Y = i.AA.Y;
        if (i.AA.Z > o.AA.Z) o.AA.Z = i.AA.Z;

        if (i.BB.X < o.BB.X) o.BB.X = i.BB.X;
        if (i.BB.Y < o.BB.Y) o.BB.Y = i.BB.Y;
        if (i.BB.Z < o.BB.Z) o.BB.Z = i.BB.Z;
        
        return true;
    }
    

    public bool IsEmpty()
    {
        return AA.X == Single.MaxValue && AA.Y == Single.MaxValue && AA.Z == Single.MaxValue
               && BB.X == Single.MinValue && BB.Y == Single.MinValue && BB.Z == Single.MinValue;
    }
    
    
    public void Reset()
    { 
        AA = new Vector3(Single.MaxValue, Single.MaxValue, Single.MaxValue);
        BB = new Vector3(Single.MinValue, Single.MinValue, Single.MinValue);
    }


    public AABB(in engine.geom.Rect2 rect2)
    {
        Reset();
        Add(new Vector3(rect2.A.X, 0f, rect2.A.Y));
        Add(new Vector3(rect2.B.X, 0f, rect2.B.Y));
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

    public AABB()
    {
        Reset();
    }
    
}