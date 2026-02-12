namespace engine.world;


/// <summary>
/// Emitted when a cluster completes lazy street generation and cluster operators.
/// </summary>
public class ClusterCompletedEvent : engine.news.Event
{
    public const string EVENT_TYPE = "world.cluster.completed";

    public ClusterCompletedEvent(string clusterName)
        : base(EVENT_TYPE, clusterName)
    {
    }
}


/// <summary>
/// Emitted when GenerateClustersOperator finishes creating all clusters.
/// Code contains the cluster count as a string.
/// </summary>
public class ClustersGeneratedEvent : engine.news.Event
{
    public const string EVENT_TYPE = "world.clusters.generated";

    public ClustersGeneratedEvent(int clusterCount)
        : base(EVENT_TYPE, clusterCount.ToString())
    {
    }
}
