using System;
using System.Numerics;
using engine.transform.components;

namespace builtin.parts;


public class TitleCard
{
    public string StartReference;
    public TimeSpan StartOffset;
    public string EndReference;
    public TimeSpan EndOffset;
    public double Duration;

    public float FadeInTime = 0.2f;

    public Vector2 Size;
    public Transform3 StartTransform;
    public Transform3 EndTransform;

    public engine.joyce.Texture AlbedoTexture;
    public engine.joyce.Texture EmissiveTexture;
    
    public Vector2 PosUV = new Vector2(0f, 1f);
    public Vector2 SizeUV = new Vector2(1f, -1f);
}