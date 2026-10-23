using System;
using System.Collections.Generic;
using System.Numerics;

namespace engine.tale;

/// <summary>A bystander near a player action: NPC id and its distance to the event.</summary>
public readonly record struct Witness(int NpcId, float Distance);


/// <summary>
/// TALE S1-b: which NPCs witness a player action at a point. Pure: the
/// caller passes the candidates' positions and, if needed, a line-of-sight
/// test (S1-d gives it a ray cast). The result does not depend on the order
/// of the candidates.
/// </summary>
public static class WitnessSelector
{
    /// <summary>
    /// Candidates within <paramref name="radius"/> of <paramref name="at"/>,
    /// except <paramref name="excludeId"/>, nearest first (ties by id), at most
    /// <paramref name="maxCount"/>. <paramref name="lineOfSight"/> (null: all
    /// can see) is asked only while walking that sorted list, until enough
    /// witnesses are found.
    /// </summary>
    public static List<Witness> Select(
        Vector3 at,
        IEnumerable<(int NpcId, Vector3 Position)> candidates,
        int excludeId,
        float radius,
        int maxCount,
        Func<int, Vector3, bool> lineOfSight = null)
    {
        var result = new List<Witness>();
        if (candidates == null || radius <= 0f || maxCount <= 0) return result;

        float radiusSq = radius * radius;
        var inRange = new List<(int NpcId, Vector3 Position, float DistanceSq)>();
        foreach (var (id, position) in candidates)
        {
            if (id == excludeId) continue;
            float dSq = Vector3.DistanceSquared(at, position);
            if (dSq <= radiusSq) inRange.Add((id, position, dSq));
        }

        inRange.Sort((a, b) =>
        {
            int c = a.DistanceSq.CompareTo(b.DistanceSq);
            return c != 0 ? c : a.NpcId.CompareTo(b.NpcId);
        });

        int lastId = int.MinValue;
        foreach (var (id, position, dSq) in inRange)
        {
            if (result.Count >= maxCount) break;
            if (id == lastId) continue;     // the same NPC listed twice
            lastId = id;
            if (lineOfSight != null && !lineOfSight(id, position)) continue;
            result.Add(new Witness(id, MathF.Sqrt(dSq)));
        }
        return result;
    }


    /// <summary>
    /// TALE S1-d: whether a ray hit blocks a witness's view. Only the environment
    /// does (buildings, bridges, movable scenery); a body without collision
    /// properties counts as static environment, as in the contact filter.
    /// NPCs, the player and vehicles do not block the view.
    /// </summary>
    public static bool IsOccluder(engine.physics.CollisionProperties props) =>
        props == null
        || (props.SolidLayerMask & (engine.physics.CollisionProperties.Layers.StaticEnvironment
                                    | engine.physics.CollisionProperties.Layers.MovableEnvironment)) != 0;
}
