using System.Threading.Tasks;

namespace engine.world;

public interface IClusterOperator
{
    public System.Func<Task> ClusterOperatorApply(ClusterDesc clusterDesc);
}
