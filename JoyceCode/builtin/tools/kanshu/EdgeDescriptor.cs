namespace builtin.tools.kanshu;

public class EdgeDescriptor<TEdgeLabel>
{
    public TEdgeLabel Label { get; set; }
    public int NodeFrom { get; set; }
    public int NodeTo { get; set; }
}


