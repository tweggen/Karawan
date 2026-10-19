using System.Globalization;

namespace engine.tale;

/// <summary>
/// The text of the per-NPC TALE debug overlay (S0 W7): two lines,
///
///   role | storylet [T2]
///   g{id} {group name} | trust {Trust[-1]}
///
/// Pure, so the overlay module only decides when to show it. ASCII
/// separators only: the OSD font (Prototype.ttf) has no middle dot.
/// </summary>
public static class TaleOverlayText
{
    /// <summary>The key under which an NPC's trust in the player is stored.</summary>
    public const int PlayerTrustKey = -1;

    public static string Format(NpcSchedule schedule, RuntimeGroup group, bool isTier2)
    {
        if (schedule == null) return "";

        string role = string.IsNullOrEmpty(schedule.Role) ? "?" : schedule.Role;
        string storylet = string.IsNullOrEmpty(schedule.CurrentStorylet) ? "-" : schedule.CurrentStorylet;
        string line1 = isTier2 ? $"{role} | {storylet} [T2]" : $"{role} | {storylet}";

        string groupPart;
        if (group != null)
        {
            groupPart = string.IsNullOrEmpty(group.Name)
                ? $"g{group.GroupId}"
                : $"g{group.GroupId} {group.Name}";
        }
        else if (schedule.GroupId >= 0)
        {
            /*
             * The schedule names a group the social state does not (yet) know.
             */
            groupPart = $"g{schedule.GroupId}";
        }
        else
        {
            groupPart = "no group";
        }

        string trustPart = schedule.Trust != null && schedule.Trust.TryGetValue(PlayerTrustKey, out float trust)
            ? "trust " + trust.ToString("F2", CultureInfo.InvariantCulture)
            : "trust -";

        return $"{line1}\n{groupPart} | {trustPart}";
    }
}
