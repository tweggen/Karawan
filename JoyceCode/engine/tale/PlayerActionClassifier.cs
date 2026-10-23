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


    /// <summary>
    /// TALE S1-d: the player's ship must have been at least this fast (m/s) before
    /// the contact for it to be a run-over; a parked or creeping ship only touches.
    /// </summary>
    public const float RunOverMinSpeed = 4f;


    /// <summary>
    /// TALE S1-d: what a contact means once the ship's speed is known. A player
    /// weapon is an assault; the player's vehicle is a run-over only at
    /// <see cref="RunOverMinSpeed"/> or faster (measured before the contact,
    /// since the post-solve velocity may already be reduced); NPC vehicles and
    /// weapons are none.
    /// </summary>
    public static PlayerAction Attribute(CollisionProperties.Layers otherLayers, float playerVehicleSpeed)
    {
        var action = Classify(otherLayers);
        if (action == PlayerAction.RunOver && playerVehicleSpeed < RunOverMinSpeed)
            return (otherLayers & CollisionProperties.Layers.PlayerWeapon) != 0 ? PlayerAction.Assault : PlayerAction.None;
        return action;
    }


    /// <summary>The interaction type id that applies the action, or null for None.</summary>
    public static string InteractionId(PlayerAction action) => action switch
    {
        PlayerAction.Assault => "assault",
        PlayerAction.RunOver => "run_over",
        _ => null
    };
}
