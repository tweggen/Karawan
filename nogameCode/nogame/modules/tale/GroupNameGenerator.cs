using builtin.tools;

namespace nogame.modules.tale;

/// <summary>
/// Deterministic faction names for the runtime group table (TALE-SOCIAL
/// Phase E2). Lives in the game layer — flavor text is game content, not
/// engine — and is handed to TaleManager via its GroupNameProvider hook.
/// Same (cluster, group) always yields the same name, so names survive
/// depopulate/repopulate and save/load without being persisted.
/// </summary>
public static class GroupNameGenerator
{
    private static readonly string[] CriminalAdjectives =
        { "Rusted", "Silent", "Broken", "Neon", "Hollow", "Burnt", "Stray", "Crooked" };
    private static readonly string[] CriminalNouns =
        { "Crew", "Syndicate", "Vipers", "Jackals", "Circuit", "Knives", "Dogs", "Serpents" };

    private static readonly string[] PatrolAdjectives =
        { "First", "Central", "Northern", "Eastern", "Southern", "Western", "Old Town", "Harbor" };
    private static readonly string[] PatrolNouns =
        { "Precinct", "Watch", "Patrol", "Division", "Guard", "Detail" };

    private static readonly string[] TradeAdjectives =
        { "Golden", "Amber", "Meridian", "Pacific", "Crystal", "Summit", "Cobalt", "Ivory" };
    private static readonly string[] TradeNouns =
        { "Consortium", "Exchange", "Traders", "Partnership", "Holdings", "Caravan" };

    private static readonly string[] SocialAdjectives =
        { "Dusty", "Late Night", "Corner", "Backstreet", "Rooftop", "Boulevard", "Oasis", "Sundown" };
    private static readonly string[] SocialNouns =
        { "Circle", "Regulars", "Club", "Crowd", "Company", "Friends" };


    public static string Generate(int clusterIndex, int groupId, string groupType)
    {
        var rnd = new RandomSource($"{clusterIndex}-group-{groupId}");
        var (adjectives, nouns) = groupType switch
        {
            "criminal" => (CriminalAdjectives, CriminalNouns),
            "patrol_unit" => (PatrolAdjectives, PatrolNouns),
            "trade" => (TradeAdjectives, TradeNouns),
            _ => (SocialAdjectives, SocialNouns)
        };
        string adjective = adjectives[rnd.GetInt(adjectives.Length)];
        string noun = nouns[rnd.GetInt(nouns.Length)];
        return $"The {adjective} {noun}";
    }
}
