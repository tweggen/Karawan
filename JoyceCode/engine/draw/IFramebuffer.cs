﻿using System;
using System.Numerics;

namespace engine.draw;

public interface IFramebuffer
{
    public uint Width { get; }
    public uint Height { get; }
    public uint Generation { get; }

    public void BeginModification();
    public void EndModification();
    public void FillRectangle(Context context, Vector2 ul, Vector2 lr);
    public void ClearRectangle(Context context, Vector2 ul, Vector2 lr);
        
    public void DrawText(Context context, Vector2 ul, Vector2 lr, string text);

    
    public void GetMemory(out Span<byte> spanBytes);
}