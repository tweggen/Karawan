using System.Numerics;
using engine.streets;
using engine.world;

namespace engine;

public class PlacementContext
{
    public Fragment? CurrentFragment;
    public ClusterDesc? CurrentCluster;
    public Quarter? CurrentQuarter;
    public Vector3 CurrentPosition;
}
