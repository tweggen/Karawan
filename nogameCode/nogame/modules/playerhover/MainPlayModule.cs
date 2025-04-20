using BepuPhysics;
using BepuPhysics.Collidables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using DefaultEcs;
using engine;
using engine.draw;
using engine.gongzuo;
using engine.joyce;
using engine.joyce.components;
using engine.news;
using engine.physics;
using engine.world;
using static engine.Logger;

namespace nogame.modules.playerhover;


/**
 * This contains player-related glue code.
 *
 * - testing what the player is seeing in front of them
 * - handling player - polytope collision
 * - playback the proper song depending on the current cluster
 * - playback sounds on player environment collisions
 * - creating particle effect on player collision
 * - playback sounds on player cube collisions
 * - manage the sound of my own car.
 * - create the ship player entity
 */
public class MainPlayModule : engine.AModule
{
    static public readonly string PhysicsName = "nogame.playerhover";

    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<nogame.modules.AutoSave>(),
        new MyModule<nogame.modules.playerhover.UpdateEmissionContext>(),
        new MyModule<nogame.modules.playerhover.ClusterMusicModule>(),
        new MyModule<nogame.modules.playerhover.HoverModule>() { ShallActivate =  false }
    };
    
    private PlayerViewer _playerViewer;
    
    private void _onLogicalFrame(object? sender, float dt)
    {
    }


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


    public override void ModuleDeactivate()
    {
        // TXWTODO: Deactivate player entity. But we don't remove the player entity at all...
        // _engine.SetPlayerEntity(new DefaultEcs.Entity());
        I.Get<MetaGen>().Loader.RemoveViewer(_playerViewer);
        
        _engine.OnLogicalFrame -= _onLogicalFrame;
        
        _engine.RemoveModule(this);

        base.ModuleDeactivate();
    }


    private async Task _setupPlayer()
    {
        var gameState = M<AutoSave>().GameState;
        Vector3 v3Ship = gameState.PlayerPosition;
        Quaternion qShip = Quaternion.Normalize(gameState.PlayerOrientation);
        if (v3Ship == Vector3.Zero)
        {
            Error($"Unbelievably could not come up with a valid start position, so generate one here.");
            _findStartPosition(out v3Ship, out qShip);
        }

        /*
         * Create the ship entiiies. This needs to run in logical thread.
         */
        _engine.QueueMainThreadAction(() =>
        {
            _engine.OnLogicalFrame += _onLogicalFrame;

            /*
             * Create a viewer for the player itself, defining what parts
             * of the world shall be loaded.
             */
            _playerViewer = new(_engine);
            I.Get<MetaGen>().Loader.AddViewer(_playerViewer);
            
            ActivateMyModule<HoverModule>();
        }); // End of queue mainthread action.
    }


    public override void ModuleActivate()
    {
        base.ModuleActivate();
        _engine.AddModule(this);

        _engine.Run(_setupPlayer);
    }
}