using System.Collections.Generic;

namespace builtin.tools;

public class SegmentRoute
{
    public int StartIndex = 0;
    public float StartRelative = 0f;
    public bool LoopSegments = false;
    public List<builtin.tools.SegmentEnd> Segments = new();
}