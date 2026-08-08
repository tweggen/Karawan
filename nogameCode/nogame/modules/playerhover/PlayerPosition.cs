using System;
using System.Collections.Generic;
using System.Numerics;
using engine;
using engine.world;
using static engine.Logger;

namespace nogame.modules.playerhover;

public class PlayerPosition : AModule
{
    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<nogame.modules.AutoSave>(),
    };

    /**
     * Find and return a suitable start position for the player.
     * We know there is a cluster around 0/0, so find it, and find an estate
     * within without a house build upon it.
     */
    private void _findStartPosition(out Vector3 v3Start, out Quaternion qStart)
    {
        ClusterDesc startCluster = I.Get<ClusterList>().GetClusterAt(Vector3.Zero);
        if (null != startCluster)
        {
            
            startCluster.FindStartPosition(out v3Start, out qStart);
            v3Start += startCluster.Pos;
            Trace($"Startposition is {v3Start} {qStart}");
        }
        else
        {
            v3Start = new Vector3(0f, 200f, 0f);
            qStart = Quaternion.Identity;
            Trace($"No start cluster found, using default startposition is {v3Start} {qStart}");
        }
    }

    
    public void GetPlayerPosition(out Vector3 Position, out Quaternion Orientation)
    {
        var gameState = M<AutoSave>().GameState;
        Vector3 v3Ship = gameState.PlayerPosition;

        /*
         * Sanitize, do NOT Normalize. This line used to read
         *
         *     Quaternion qShip = Quaternion.Normalize(gameState.PlayerOrientation);
         *
         * which reads like validation and is not. On the all-zero default(Quaternion) that a
         * missing or never-written save field produces, Normalize computes 1/sqrt(0) = Infinity
         * and then 0 * Infinity = NaN - so it turns one invalid quaternion into another while
         * destroying the evidence that it was zero. NaN input passes straight through for the
         * same reason.
         *
         * The value goes to engine.physics.actions.CreateDynamic, and BepuPhysics.Bodies.Add
         * asserts "Orientation should be initialized to a unit length quaternion." A failed
         * Debug.Assert on Android aborts the process rather than printing, so a single bad
         * persisted orientation is a BOOT LOOP: load state, build the player body, abort,
         * relaunch, load the same state. Observed 2026-08-08 in HoverModule._setupPlayer.
         *
         * See builtin.tools.SafeOrientation and its tests.
         */
        Quaternion qShip = builtin.tools.SafeOrientation.Sanitize(
            gameState.PlayerOrientation, "PlayerPosition.GetPlayerPosition");

        /*
         * A non-finite position is equally fatal and equally unrecoverable from, so it gets the
         * same treatment as the "no position at all" case: throw it away and generate a fresh
         * start rather than hand it to the physics.
         */
        bool isPositionUsable = Single.IsFinite(v3Ship.X)
                                && Single.IsFinite(v3Ship.Y)
                                && Single.IsFinite(v3Ship.Z);
        if (!isPositionUsable)
        {
            Error($"Saved player position {v3Ship} is not finite, generating a new start position.");
        }

        if (v3Ship == Vector3.Zero || !isPositionUsable)
        {
            Error($"Unbelievably could not come up with a valid start position, so generate one here.");
            _findStartPosition(out v3Ship, out qShip);
            qShip = builtin.tools.SafeOrientation.Sanitize(qShip, "PlayerPosition._findStartPosition");
        }

        Position = v3Ship;
        Orientation = qShip;
    }
    
}