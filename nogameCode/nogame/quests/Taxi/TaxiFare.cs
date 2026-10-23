using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using engine;
using engine.tale;
using static engine.Logger;

namespace nogame.quests.Taxi;

/// <summary>
/// TALE S1-g: what a finished taxi run earns — the player's money source for
/// paying and bribing NPCs (GameState.Cash, owner 2026-10-18) — and which TALE
/// NPC stands for the passenger, so the lift counts with them.
/// </summary>
public static class TaxiFare
{
    public const int BaseFare = 30;

    /// <summary>One unit of cash per this many metres driven, on top of the base fare.</summary>
    public const float MetresPerCash = 10f;

    /// <summary>A materialised TALE NPC this close to the pickup becomes the passenger.</summary>
    public const float PassengerRadius = 30f;


    public static int Of(float fareDistance) => BaseFare + (int)(Math.Max(0f, fareDistance) / MetresPerCash);


    /// <summary>Horizontal distance from pickup to destination.</summary>
    public static float DistanceOf(Vector3 pickup, Vector3 destination) =>
        Vector2.Distance(new Vector2(pickup.X, pickup.Z), new Vector2(destination.X, destination.Z));


    /// <summary>
    /// The nearest materialised TALE NPC within PassengerRadius of
    /// <paramref name="pickup"/>, ties by id; false if none. Logical thread.
    /// </summary>
    public static bool TryFindPassenger(Vector3 pickup, out int npcId)
    {
        npcId = 0;
        var candidates = new List<(int, Vector3)>();
        foreach (var e in I.Get<Engine>().GetEcsWorld().GetEntities()
                     .With<engine.tale.components.TaleNpcId>()
                     .With<engine.joyce.components.Transform3ToWorld>().AsEnumerable())
        {
            candidates.Add((e.Get<engine.tale.components.TaleNpcId>().NpcId,
                e.Get<engine.joyce.components.Transform3ToWorld>().Matrix.Translation));
        }

        var nearest = WitnessSelector.Select(pickup, candidates, int.MinValue, PassengerRadius, 1);
        if (nearest.Count == 0) return false;
        npcId = nearest[0].NpcId;
        return true;
    }


    /// <summary>
    /// The run succeeded: add the fare to GameState.Cash and, if a TALE NPC was
    /// the passenger, apply "drive" to them. Logical thread.
    /// </summary>
    public static void Pay(in TaxiQuestData data)
    {
        int fare = Of(data.FareDistance);
        var gameState = nogame.modules.tale.TaleNarrationBindings.TryGetGameState();
        if (gameState == null)
        {
            Warning($"Taxi: no game state yet, fare of {fare} not paid.");
        }
        else
        {
            gameState.Cash += fare;
            Trace($"Taxi: fare {fare} for {data.FareDistance:F0} m; cash now {gameState.Cash}.");
        }

        if (!data.HasPassengerNpc) return;
        try
        {
            var r = I.Get<TaleManager>().ApplyPlayerInteraction(data.PassengerNpcId, "drive",
                nogame.modules.tale.TaleNarrationBindings.GameNow());
            Trace($"Taxi: passenger NPC {data.PassengerNpcId} trust {r.TrustBefore:F2} -> {r.TrustAfter:F2}.");
        }
        catch (Exception e)
        {
            Warning($"Taxi: could not credit the lift to NPC {data.PassengerNpcId}: {e.Message}");
        }
    }
}
