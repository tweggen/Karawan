using System.Collections.Generic;

namespace engine.tale;

/// <summary>
/// One group found by CommunityDetector (DES bake and runtime re-detection).
/// </summary>
public class DetectedGroup
{
    public int GroupId;
    public string Type; // "criminal", "trade", "social", "patrol_unit"
    public List<int> MemberIds;
}

/// <summary>
/// The result of one CommunityDetector run, also fed to SimMetrics.OnGroupDetection.
/// (Its first producer, the Bron-Kerbosch GroupDetector, was replaced in
/// TALE-SOCIAL Phase E and removed in KAR-430.)
/// </summary>
public class GroupDetectionResult
{
    public int TotalGroups;
    public Dictionary<string, int> GroupsByType = new();
    public int LargestGroupSize;
    public int NpcsInGroups;
    public List<DetectedGroup> Groups = new();
}
