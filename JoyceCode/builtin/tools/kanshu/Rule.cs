namespace builtin.tools.kanshu;

public class Rule<TNodeLabel,TEdgeLabel>
{
    public Pattern<TNodeLabel, TEdgeLabel> Pattern { get; init; }
    public Graph<TNodeLabel, TEdgeLabel> Replacement { get; init; }
}