using System;
using System.Numerics;

namespace engine.behave.components;

public struct Clickable
{
    /**
     * The index of the camera layer this clickable is in (not the mask).
     */
    public byte CameraLayer;

    [Flags]
    public enum ClickableFlags : byte
    {
        AlsoOnRelease = 1
    };
    public ClickableFlags Flags;

    public Func<DefaultEcs.Entity, engine.news.Event, Vector2, engine.news.Event> ClickEventFactory;
}
