using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace nogame.quests.HelloFishmonger;


/**
 * What a save keeps of a running HelloFishmonger chase (KAR-475): which cluster the
 * fishmonger's car drives in and the junction it was driving away from. On load the car
 * is created again at that junction and drives on from there - at most one street's
 * length behind where it was.
 *
 * The junction is kept by its position in the cluster, never by id: ids name different
 * junctions once a cluster is regenerated.
 *
 * Plain System.* types only, so the round trip is tested without the game.
 */
public readonly record struct HelloFishmongerSaveState(int ClusterIndex, Vector2 DrivingFrom)
{
    public JsonNode ToJson() => new JsonObject
    {
        ["clusterIndex"] = ClusterIndex,
        ["drivingFromX"] = DrivingFrom.X,
        ["drivingFromY"] = DrivingFrom.Y
    };


    /**
     * Null when the element holds no chase - e.g. it was saved while the car was gone -
     * in which case the quest cannot be resumed.
     */
    public static HelloFishmongerSaveState? FromJson(JsonElement je)
    {
        if (je.ValueKind != JsonValueKind.Object) return null;
        if (!je.TryGetProperty("clusterIndex", out var jeCluster)
            || !je.TryGetProperty("drivingFromX", out var jeX)
            || !je.TryGetProperty("drivingFromY", out var jeY))
        {
            return null;
        }
        if (jeCluster.ValueKind != JsonValueKind.Number
            || jeX.ValueKind != JsonValueKind.Number
            || jeY.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return new HelloFishmongerSaveState(jeCluster.GetInt32(), new Vector2(jeX.GetSingle(), jeY.GetSingle()));
    }
}
