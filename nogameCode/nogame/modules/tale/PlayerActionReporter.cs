using System;
using System.Collections.Generic;
using System.Numerics;
using engine;
using engine.physics;
using engine.tale;
using static engine.Logger;

namespace nogame.modules.tale;

/// <summary>
/// TALE S1-d: turns a Hit or Crash the player caused (CitizenCollisionRouter's
/// Code = "player") into an assault or run-over on the TALE NPC, and lets the
/// materialised TALE NPCs nearby witness it. Witnesses need a clear line of
/// sight: one ray from their eyes to the event, blocked only by the
/// environment. Logical thread only (collision events drain there).
/// </summary>
public static class PlayerActionReporter
{
    private static readonly engine.Dc _dc = engine.Dc.TaleSocial;

    /// <summary>Eye height of a witness and the height the event is seen at, metres.</summary>
    private const float EyeHeight = 1.6f;
    private const float EventHeight = 1.0f;


    public static void Report(int victimId, PlayerAction action, Vector3 at)
    {
        string id = PlayerActionClassifier.InteractionId(action);
        if (id == null) return;

        try
        {
            var tm = I.Get<TaleManager>();
            var report = tm.ReportPlayerAction(victimId, id, at, TaleNarrationBindings.GameNow(),
                _materialisedTaleNpcs(), _lineOfSight(at));
            if (report.Victim.Applied)
            {
                Trace(_dc, $"player {id} on NPC {victimId}: trust {report.Victim.TrustBefore:F2} -> "
                           + $"{report.Victim.TrustAfter:F2}, {report.Witnesses.Count} witness(es)");
            }
        }
        catch (Exception e)
        {
            Error($"TALE: reporting player {id} on NPC {victimId} failed: {e.Message}");
        }
    }


    private static IEnumerable<(int NpcId, Vector3 Position)> _materialisedTaleNpcs()
    {
        var list = new List<(int, Vector3)>();
        foreach (var e in I.Get<Engine>().GetEcsWorld().GetEntities()
                     .With<engine.tale.components.TaleNpcId>()
                     .With<engine.joyce.components.Transform3ToWorld>().AsEnumerable())
        {
            list.Add((e.Get<engine.tale.components.TaleNpcId>().NpcId,
                e.Get<engine.joyce.components.Transform3ToWorld>().Matrix.Translation));
        }
        return list;
    }


    private static Func<int, Vector3, bool> _lineOfSight(Vector3 at)
    {
        var physics = I.Get<engine.physics.API>();
        Vector3 target = at + EventHeight * Vector3.UnitY;
        return (_, position) =>
        {
            Vector3 eye = position + EyeHeight * Vector3.UnitY;
            Vector3 ray = target - eye;
            bool blocked = false;
            // RayCastSync takes the simulation lock itself; t runs from 0 (eye) to 1 (event).
            physics.RayCastSync(eye, ray, 1f, (_, props, t, _) =>
            {
                if (t > 0f && t < 1f && WitnessSelector.IsOccluder(props)) blocked = true;
            });
            return !blocked;
        };
    }
}
