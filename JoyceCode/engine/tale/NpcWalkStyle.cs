using System;

namespace engine.tale;


/**
 * How one NPC walks, apart from where it goes: how far onto the pavement it keeps, how fast
 * it is, and how much its departures and its place along a route differ from another NPC's.
 *
 * KAR-481: walking TALE NPCs stood and moved on exactly the same spot. Every one walked at
 * the same speed (4.5 km/h), on the same deterministic route between shared doors, 1.5 m
 * off the kerb, and left together - so NPCs sharing an origin and a destination were one
 * figure for the whole walk, and the player saw them only when talking to one "forked it
 * off". This spreads them without any collision detection.
 *
 * Derived from the NPC id alone, with its own seed, so the same NPC always walks the same
 * way, nothing is saved, and no other random draw of the NPC moves.
 */
public readonly record struct NpcWalkStyle(
    float SidewalkOffset, float SpeedFactor, float DepartureJitterSeconds, float RoutePhaseMetres)
{
    /*
     * Round PedestrianRoute.SidewalkOffset (1.5 m), no wider: a 1 m pavement already has
     * the fixed offset against the building line.
     */
    public const float MinSidewalkOffset = 1.0f;
    public const float MaxSidewalkOffset = 2.0f;

    public const float SpeedSpread = 0.15f;
    public const float DepartureSpreadSeconds = 15f;
    public const float RoutePhaseSpreadMetres = 15f;


    /*
     * Not builtin.tools.RandomSource: its string seed is a weighted sum of the characters,
     * and over one city's ids it gave 208 different styles for 279 NPCs. SplitMix64 over the
     * id is uniform, platform independent and cheap.
     */
    private static ulong _splitMix(ref ulong state)
    {
        ulong z = (state += 0x9E3779B97F4A7C15UL);
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }


    public static NpcWalkStyle For(int npcId)
    {
        ulong state = 0x5EED_7A1E_0000_0000UL ^ (uint)npcId;
        float Unit() => (_splitMix(ref state) >> 40) / (float)(1UL << 24);    // [0, 1)
        float Signed() => Unit() * 2f - 1f;

        float offset = MinSidewalkOffset + Unit() * (MaxSidewalkOffset - MinSidewalkOffset);
        float speed = 1f + Signed() * SpeedSpread;
        float departure = Signed() * DepartureSpreadSeconds;
        float phase = Signed() * RoutePhaseSpreadMetres;
        return new NpcWalkStyle(offset, speed, departure, phase);
    }


    /**
     * The real seconds an activity lasts for this NPC: the clamped storylet duration, then
     * the NPC's own jitter. AFTER the clamp, because every storylet over two game hours
     * clamps to the same 300 s, and jitter added before it would be clamped away - those
     * NPCs would still all leave in one frame.
     */
    public float StayDurationSeconds(float clampedRealSeconds)
        => Math.Max(2f, clampedRealSeconds + DepartureJitterSeconds);


    /**
     * Where along a route of <paramref name="routeLength"/> metres this NPC is, given the
     * fraction <paramref name="t"/> the schedule says: shifted by its own phase, within the
     * route.
     */
    public float RouteFraction(float t, float routeLength)
    {
        if (routeLength <= 0f) return Math.Clamp(t, 0f, 1f);
        return Math.Clamp(t + RoutePhaseMetres / routeLength, 0f, 1f);
    }
}
