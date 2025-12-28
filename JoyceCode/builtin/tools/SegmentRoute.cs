using System.Collections.Generic;

namespace builtin.tools;

public class SegmentRoute
{
    public bool LoopSegments = false;
    public List<builtin.tools.SegmentEnd> Segments = new();
}