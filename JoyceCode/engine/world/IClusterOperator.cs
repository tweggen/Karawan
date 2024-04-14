using System.Threading.Tasks;

namespace engine.world;

public interface IClusterOperator
{
    public void ClusterOperatorApply(ClusterDesc clusterDesc);
}
