using System.Numerics;
using engine.world;

namespace builtin.modules;

public class ClusterWatchEvent : engine.news.Event
{
    public ClusterDesc CurrentCluster { get; set; }
    public Vector3 PlayerPos { get; set; }
 
    public static readonly string CLUSTER_CHANGED = "cluster.watch.changed";
    
    public ClusterWatchEvent(string type, string code) : base(type, code)
    {
    }
}