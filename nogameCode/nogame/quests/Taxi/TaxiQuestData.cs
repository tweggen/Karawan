using System.Numerics;
using System.Text.Json.Serialization;

namespace nogame.quests.Taxi;

[engine.IsPersistable]
public struct TaxiQuestData
{
    [JsonInclude] public Vector3 GuestPosition;
    [JsonInclude] public Vector3 DestinationPosition;
    [JsonInclude] public byte Phase; // 0 = pickup, 1 = driving
}
