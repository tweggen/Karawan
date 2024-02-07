using System;
using System.Numerics;
using engine.joyce.components;

namespace builtin.parts;


public class TitleCard
{
    public enum F
    {
        JitterEnd = 1
    };
    public string StartReference;
    public TimeSpan StartOffset;
    public string EndReference;
    public TimeSpan EndOffset;
    public double Duration;

    public uint Flags;

    // public float FadeOutTime = 1.2f;

    public Vector2 Size;
    public Transform3 StartTransform;
    public Transform3 EndTransform;

    public engine.joyce.Texture AlbedoTexture;
    public engine.joyce.Texture EmissiveTexture;
    
    public Vector2 PosUV = new Vector2(0f, 1f);
    public Vector2 SizeUV = new Vector2(1f, -1f);
}