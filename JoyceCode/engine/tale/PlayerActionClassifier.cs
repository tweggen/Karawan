using engine.physics;

namespace engine.tale;

/// <summary>A world action of the player against an NPC, as a contact reveals it.</summary>
public enum PlayerAction
{
    None,
    Assault,
    RunOver
}


/// <summary>
/// TALE S1-b: what a contact on an NPC means, from the layers of the other
/// body. A player weapon (melee or bullet) is an assault, the player's
/// vehicle is a run-over; NPC vehicles, NPC weapons and anything else are
/// not the player's doing. If a body carries both player bits, the run-over
/// wins as the graver act. Whether the ship was fast enough to count is the
/// caller's check (S1-d).
/// </summary>
public static class PlayerActionClassifier
{
    public static PlayerAction Classify(CollisionProperties.Layers otherLayers)
    {
        if ((otherLayers & CollisionProperties.Layers.PlayerVehicle) != 0) return PlayerAction.RunOver;
        if ((otherLayers & CollisionProperties.Layers.PlayerWeapon) != 0) return PlayerAction.Assault;
        return PlayerAction.None;
    }


    /// <summary>The interaction type id that applies the action, or null for None.</summary>
    public static string InteractionId(PlayerAction action) => action switch
    {
        PlayerAction.Assault => "assault",
        PlayerAction.RunOver => "run_over",
        _ => null
    };
}
