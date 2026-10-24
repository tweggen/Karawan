using System;
using System.Collections.Generic;

namespace engine.tale;

/// <summary>
/// TALE S1-f: the player's standing level in one cluster, a reading of the
/// society's state (owner, 2026-10-18; NPC_STORIES' social-capital levels).
/// Levels gate what the society offers, they are not an XP bar.
/// </summary>
public enum StandingLevel
{
    Anonymous = 0,
    Recognised = 1,
    Connected = 2,
    Entangled = 3
}


/// <summary>
/// First-guess thresholds, tunable. Each level has an enter and a lower
/// leave condition (hysteresis), so a level does not flicker on one change.
/// </summary>
public sealed class StandingThresholds
{
    /* Recognised: enough NPCs know the player well — or fear them (notoriety counts). */
    public float RecognisedLiked = 0.30f;
    public float RecognisedFeared = 0.05f;
    public int RecognisedEnterCount = 3;
    public float RecognisedStayLiked = 0.26f;
    public float RecognisedStayFeared = 0.08f;
    public int RecognisedStayCount = 2;

    /* Connected: a group holds a clear opinion of the player, positive or negative. */
    public float ConnectedStanding = 0.15f;
    public float ConnectedStayStanding = 0.10f;
    public int ConnectedMinKnown = 2;

    /* Entangled: favours both ways and standing with rival groups (S3, S4; 0 in S1). */
    public int EntangledFavourDirections = 2;
    public int EntangledRivalGroups = 2;
}


/// <summary>What the level is read from; the enter and stay counts of each condition.</summary>
public readonly record struct StandingInputs(
    int KnownEnter, int KnownStay, int GroupsEnter, int GroupsStay, int FavourDirections, int RivalGroups);


/// <summary>A cluster's standing level and the inputs it came from.</summary>
public readonly record struct StandingReading(StandingLevel Level, StandingInputs Inputs)
{
    public string Name => PlayerStandingLevel.Name(Level);
}


public static class PlayerStandingLevel
{
    public static string Name(StandingLevel level) => level switch
    {
        StandingLevel.Recognised => "recognised",
        StandingLevel.Connected => "connected",
        StandingLevel.Entangled => "entangled",
        _ => "anonymous"
    };


    /// <summary>
    /// Count the inputs over a cluster's NPCs and its groups' standings. Only
    /// NPCs that know the player count; a group counts with at least
    /// ConnectedMinKnown members who know the player. Favours (S3) and rival
    /// groups (S4) do not exist yet: 0.
    /// </summary>
    public static StandingInputs Measure(IEnumerable<NpcSchedule> npcs, IEnumerable<GroupStanding> groups,
        StandingThresholds t)
    {
        int knownEnter = 0, knownStay = 0;
        if (npcs != null)
        {
            foreach (var npc in npcs)
            {
                if (!PlayerTrust.Knows(npc)) continue;
                float trust = PlayerTrust.Get(npc);
                if (trust >= t.RecognisedLiked || trust <= t.RecognisedFeared) knownEnter++;
                if (trust >= t.RecognisedStayLiked || trust <= t.RecognisedStayFeared) knownStay++;
            }
        }

        int groupsEnter = 0, groupsStay = 0;
        if (groups != null)
        {
            foreach (var g in groups)
            {
                if (g.Known < t.ConnectedMinKnown) continue;
                float abs = Math.Abs(g.Standing);
                if (abs >= t.ConnectedStanding) groupsEnter++;
                if (abs >= t.ConnectedStayStanding) groupsStay++;
            }
        }

        return new StandingInputs(knownEnter, knownStay, groupsEnter, groupsStay, 0, 0);
    }


    /// <summary>
    /// The highest level whose enter condition holds, or which was held
    /// before (<paramref name="previous"/>) and whose stay condition still
    /// holds. Without a previous level (first visit, after a load) only the
    /// enter conditions apply.
    /// </summary>
    public static StandingLevel Evaluate(StandingInputs i, StandingLevel? previous, StandingThresholds t)
    {
        bool Held(StandingLevel level, bool enter, bool stay) =>
            enter || (previous.HasValue && previous.Value >= level && stay);

        if (Held(StandingLevel.Entangled,
                i.FavourDirections >= t.EntangledFavourDirections && i.RivalGroups >= t.EntangledRivalGroups,
                i.FavourDirections >= 1 && i.RivalGroups >= 1))
            return StandingLevel.Entangled;
        if (Held(StandingLevel.Connected, i.GroupsEnter >= 1, i.GroupsStay >= 1))
            return StandingLevel.Connected;
        if (Held(StandingLevel.Recognised, i.KnownEnter >= t.RecognisedEnterCount, i.KnownStay >= t.RecognisedStayCount))
            return StandingLevel.Recognised;
        return StandingLevel.Anonymous;
    }
}


/// <summary>
/// Remembers what was last published, so the dialogue props change only on
/// a transition (setting a prop copies and emits every time).
/// </summary>
public sealed class StandingPublisher
{
    private StandingLevel? _last;

    /// <summary>True if <paramref name="level"/> differs from the last one published; records it.</summary>
    public bool Changed(StandingLevel level)
    {
        if (_last == level) return false;
        _last = level;
        return true;
    }
}
