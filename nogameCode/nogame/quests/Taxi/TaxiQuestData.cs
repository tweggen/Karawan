using System.Numerics;
using System.Text.Json.Serialization;

namespace nogame.quests.Taxi;

[engine.IsPersistable]
public struct TaxiQuestData
{
    [JsonInclude] public Vector3 GuestPosition;
    [JsonInclude] public Vector3 DestinationPosition;
    [JsonInclude] public byte Phase; // 0 = pickup, 1 = driving

    /*
     * TALE S1-g. Saves from before have none of these: FareDistance 0 pays the
     * base fare, and no passenger is bound.
     */

    /// <summary>Metres from pickup to destination, measured when the quest starts; the fare depends on it.</summary>
    [JsonInclude] public float FareDistance;

    /// <summary>A TALE NPC stands for the passenger (PassengerNpcId is valid; 0 is a valid id).</summary>
    [JsonInclude] public bool HasPassengerNpc;

    [JsonInclude] public int PassengerNpcId;
}
