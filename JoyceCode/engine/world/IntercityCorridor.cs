using System;
using System.Collections.Generic;
using System.Numerics;

namespace engine.world;


/**
 * The intercity corridors as highways for the player's ship (KAR-483): where a corridor is,
 * how much it boosts the ship, and how it helps the ship keep its lane.
 *
 * A corridor is the band IntercityTrackElevationOperator flattens between the two stations
 * of an intercity line: everything within two elevation grid steps of the station axis is
 * set to the line's one track height, which cuts a groove through the terrain. Nothing is
 * drawn there and there are no lanes; the lane this assists is defined here - the middle of
 * the groove (LaneOffset).
 *
 * Pure arithmetic over plan positions, so it is tested without a game.
 */
public static class IntercityCorridor
{
    /**
     * One corridor: the two station positions in plan and the line's track height.
     */
    public readonly record struct Line(Vector2 A, Vector2 B, float TrackHeight);


    /**
     * Where a position lies in a corridor.
     * Along: metres from station A along the axis. Lateral: metres to the RIGHT of the A->B
     * axis (negative is left). Axis: unit A->B in plan.
     */
    public readonly record struct Position(int LineIndex, float Along, float Length, float Lateral,
        Vector2 Axis, float HeightAboveTrack);


    /** The elevation grid step: FragmentSize / GroundResolution (20 m). */
    public static float GridStep => MetaGen.FragmentSize / MetaGen.GroundResolution;

    /**
     * How far either side of the station axis IntercityTrackElevationOperator flattens:
     * three grid steps (60 m).
     *
     * The terrain between samples is interpolated over triangles, so a point is on flat
     * ground only where all three corners of its triangle were flattened - at worst a cell
     * diagonal inside the carved band. Two steps (40 m), as before, left as little as 12 m
     * of flat floor beside the axis over the shipped lines; three leave at least 31.5 m.
     */
    public static float CarveHalfWidth => 3f * GridStep;

    /**
     * The corridor: the floor guaranteed flat whatever the line's direction - the carved
     * band less a cell diagonal (~31.7 m either side). Beyond it is interpolated slope.
     */
    public static float HalfWidth => CarveHalfWidth - GridStep * MathF.Sqrt(2f);

    /**
     * The carving rule IntercityTrackElevationOperator applies to every elevation sample:
     * flattened when within CarveHalfWidth of the infinite line through the stations.
     * Shared, so the band the corridor assumes is the band the terrain gets.
     */
    public static bool IsInCarvedBand(in Vector2 sample, in Vector2 stationA, in Vector2 stationB)
    {
        var ab = stationB - stationA;
        float length = ab.Length();
        if (length < 1e-3f) return false;
        float lateral = Vector2.Dot(sample - stationA, RightOf(ab / length));
        return MathF.Abs(lateral) <= CarveHalfWidth;
    }


    /**
     * Whether a rectangle (a fragment) holds any of a line's band between its stations,
     * i.e. whether the operator must run on it. The operator used to ask whether the line's
     * own +-2.5 m edges crossed the rectangle's border, inside the stations' bounding box -
     * so where the axis ran within a band's width of a fragment border the neighbour's
     * share was never carved (179 samples on 24 shipped lines at 40 m, 429 on 39 at 60 m).
     */
    public static bool BandTouches(in Vector2 min, in Vector2 max, in Vector2 stationA, in Vector2 stationB)
    {
        var ab = stationB - stationA;
        float length = ab.Length();
        if (length < 1e-3f) return false;
        var axis = ab / length;
        var right = RightOf(axis);

        float latMin = Single.MaxValue, latMax = Single.MinValue, alongMin = Single.MaxValue, alongMax = Single.MinValue;
        foreach (var c in new[] { min, new Vector2(min.X, max.Y), max, new Vector2(max.X, min.Y) })
        {
            float lat = Vector2.Dot(c - stationA, right);
            float along = Vector2.Dot(c - stationA, axis);
            latMin = MathF.Min(latMin, lat);
            latMax = MathF.Max(latMax, lat);
            alongMin = MathF.Min(alongMin, along);
            alongMax = MathF.Max(alongMax, along);
        }

        return latMin <= CarveHalfWidth && latMax >= -CarveHalfWidth
            && alongMin <= length && alongMax >= 0f;
    }


    /** The ship counts as in the corridor up to this far above the track (it hovers ~3 m). */
    public const float MaxHeightAboveTrack = 15f;

    /** How much faster the ship is in a corridor (owner: three times). */
    public const float BoostFactor = 3f;

    /** Over how many metres from either station the boost builds up. */
    public const float RampMetres = 150f;

    /** How fast, in factor per second, a boost runs out once the ship is out of it. */
    public const float BoostDecayPerSecond = 0.5f;

    /**
     * The lane centre: this far right of the axis in the direction of travel. Zero - the
     * middle of the groove (KAR-484): it carries no traffic and reads as one lane, and its
     * carved flat is only about +-30 m, so the 20 m keep-right lane KAR-483 started with ran
     * along its edge. Keep-right comes back with oncoming traffic, by setting this.
     */
    public const float LaneOffset = 0f;

    /** How fast the assist turns towards the lane, per radian off, at most MaxAssistTurnRate. */
    public const float AssistGain = 1.5f;
    public const float MaxAssistTurnRate = 0.6f;     // rad/s; the player's own turn is ~2.1

    /** Below this speed along its nose the ship is parked or manoeuvring: no assist. */
    public const float MinAssistSpeed = 5f;


    /**
     * The corridor <paramref name="v3"/> is in, if any: between the stations, within the
     * flattened band, and not high above the track. The nearest axis wins where two meet.
     */
    public static bool TryLocate(in Vector3 v3, IReadOnlyList<Line> lines, out Position position)
    {
        position = default;
        if (lines == null) return false;

        var p = new Vector2(v3.X, v3.Z);
        float best = Single.MaxValue;
        bool found = false;

        for (int i = 0; i < lines.Count; ++i)
        {
            var line = lines[i];
            var ab = line.B - line.A;
            float length = ab.Length();
            if (length < 1f) continue;

            var axis = ab / length;
            float along = Vector2.Dot(p - line.A, axis);
            if (along < 0f || along > length) continue;

            float lateral = Vector2.Dot(p - line.A, RightOf(axis));
            if (MathF.Abs(lateral) > HalfWidth) continue;

            float above = v3.Y - line.TrackHeight;
            if (above > MaxHeightAboveTrack || above < -MaxHeightAboveTrack) continue;

            if (MathF.Abs(lateral) < best)
            {
                best = MathF.Abs(lateral);
                position = new Position(i, along, length, lateral, axis, above);
                found = true;
            }
        }

        return found;
    }


    /**
     * Right of a plan direction, in the engine's frame (Y up, right handed): facing -Z,
     * right is +X. As a 3-D vector it is Cross(forward, UnitY).
     */
    public static Vector2 RightOf(in Vector2 forward) => new(-forward.Y, forward.X);


    /** Smoothstep 0 -> 1 over [0, 1]. */
    private static float _smooth(float x)
    {
        x = Math.Clamp(x, 0f, 1f);
        return x * x * (3f - 2f * x);
    }


    /** 0 at either station, 1 once RampMetres in. */
    public static float RampOf(in Position position) =>
        _smooth(MathF.Min(position.Along, position.Length - position.Along) / RampMetres);


    /** The boost the ship should have here: 1 outside, up to BoostFactor inside. */
    public static float TargetBoost(in Position? position) =>
        position is { } p ? 1f + (BoostFactor - 1f) * RampOf(p) : 1f;


    /**
     * The boost after <paramref name="dt"/>: it rises with the corridor's own ramp, and when
     * the target drops - at a station, or leaving the band sideways - it runs out at
     * BoostDecayPerSecond rather than at once, so the ship is not braked like a wall.
     */
    public static float NextBoost(float current, float target, float dt)
    {
        if (target >= current) return target;
        return MathF.Max(target, current - BoostDecayPerSecond * dt);
    }


    /**
     * The lane centre's lateral offset (right of the A->B axis) for a ship facing
     * <paramref name="front"/>: right of the axis in its direction of travel.
     */
    public static float LaneCentreOf(in Position position, in Vector2 front) =>
        Vector2.Dot(front, position.Axis) >= 0f ? LaneOffset : -LaneOffset;


    /**
     * The turn, in radians this frame (positive is right, as HoverController's own), that
     * steers the ship towards its lane and along the corridor.
     *
     * It aims at a point ahead on the lane centre. It yields: scaled by how little the
     * player is steering (|playerTurn| is 0..1), and nothing at all below MinAssistSpeed or
     * when the ship points across the corridor rather than along it - leaving on purpose.
     */
    public static float AssistTurn(in Position position, in Vector2 front, float speedAlongFront,
        float playerTurn, float dt)
    {
        if (speedAlongFront < MinAssistSpeed) return 0f;

        float alongness = Vector2.Dot(front, position.Axis);
        if (MathF.Abs(alongness) < 0.5f) return 0f;

        float travel = alongness >= 0f ? 1f : -1f;
        var axisRight = RightOf(position.Axis);
        float error = LaneCentreOf(position, front) - position.Lateral;

        float lookahead = MathF.Max(30f, speedAlongFront * 1.5f);
        var desired = Vector2.Normalize(position.Axis * (travel * lookahead) + axisRight * error);

        var right = RightOf(front);
        float angle = MathF.Atan2(Vector2.Dot(desired, right), Vector2.Dot(desired, front));

        float rate = Math.Clamp(angle * AssistGain, -MaxAssistTurnRate, MaxAssistTurnRate);
        float yielding = 1f - Math.Clamp(MathF.Abs(playerTurn), 0f, 1f);
        return rate * yielding * RampOf(position) * dt;
    }
}
