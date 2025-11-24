using System.Numerics;

namespace engine;

/**
 * Describe the placement of something relative to the world
 * and the entities it contains.
 *
 * When placing something, you can either go straight for a Vector3 position,
 * or have a PositionDescription generated, which contains a bit more
 * meta-information.
 */
public class PlacementDescription
{
    public enum Reference
    {
        /**
         * The thing is placed in absolute coordinates.
         * Caution, this is rarely applicable in the procedurally generated
         * world.
         */
        World,
        
        /**
         * The thing is placed relative to the cluster.
         * Caution, without knowing how the cluster is populated, this
         * can overlap with other things.
         */
        Cluster,
        
        /**
         * The thing is placed relative to a quarter.
         * You should make sure the cluster is reasonably empty
         * before placing anything.
         */
        Quarter,
        
        /**
         * The thing is placed relative to a streetpoint.
         */
        StreetPoint
    }

    public Reference ReferenceObject;

    public enum ClusterSelection
    {
        AnyCluster,
        CurrentCluster,
        ConnectedCluster
    }

    public ClusterSelection WhichCluster;
    public uint ClusterAttributesValue;
    public uint ClusterAttributesMask;

    public enum QuarterSelection
    {
        AnyQuarter,
        CurrentQuarter,
        NearbyQuarter
    }

    public QuarterSelection WhichQuarter;
    
    /**
     * Only place in quarters with the given tag.
     */
    public string QuarterTag;
    public uint QuarterAttributesValue;
    public uint QuarterAttributesMask;

    public enum Placement
    {
        /**
         * Place the thing random with reference to the containing entity.
         * The thing is guaranteed to be placed "within" the containing entity.
         */
        Random = 0,
        Absoplute = 1,
    }

    /**
     * Where should this thing be placed with respect to the
     * reference object.
     */
    public Placement LocalPlacement;

    /**
     * The offset or position of the thing to place.
     *
     * This position is understood relative to the reference object.
     */
    public Vector3 Position;

    /**
     * The size of the thing to place. We guarantee the thing to be
     * completely "inside" the reference objecr.
     */
    public Vector3 PlacedSize;

    /**
     * The orientation of the thing to place.
     */
    public Quaternion Orientation;
}