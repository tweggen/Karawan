using System;
using System.Numerics;
using engine.joyce;

namespace Splash;

public class AMeshParams : IEquatable<AMeshParams>
{
    public engine.joyce.Mesh JMesh;
    public Vector2 UVOffset;
    public Vector2 UVScale;
    public ModelAnimation ModelAnimation;
    public int ModelAnimationFrame;

    public bool Equals(AMeshParams other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return 
            Equals(JMesh, other.JMesh)
            && ModelAnimation == other.ModelAnimation
            && ModelAnimationFrame == other.ModelAnimationFrame
            && UVOffset.Equals(other.UVOffset)
            && UVScale.Equals(other.UVScale);
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
        return HashCode.Combine(JMesh, UVOffset, UVScale, ModelAnimation, ModelAnimationFrame);
    }
}