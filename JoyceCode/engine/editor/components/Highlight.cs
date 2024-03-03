using System;

namespace engine.editor.components;

public struct Highlight
{
    [Flags]
    public enum StateFlags {
        IsSelected = 0x0001,
        IsFocussed = 0x0002 
    }
    public byte Flags;
    public uint Color;
}