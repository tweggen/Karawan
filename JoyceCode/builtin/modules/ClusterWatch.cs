using System.Numerics;
using engine;
using engine.draw;
using engine.joyce.components;
using engine.news;
using engine.world;

namespace builtin.modules;

public class ClusterWatch : AController
{
    private ClusterDesc _currentCluster = null;

    private DefaultEcs.Entity _ePlayer;
    private Vector3 _v3Player;


    private void _onPlayerEntityChanged(DefaultEcs.Entity entity)
    {
        bool isChanged = false;
        lock (_lo)
        {
            if (_ePlayer != entity)
            {
                _ePlayer = entity;
                isChanged = true;
            }
        }
    }
    
    
    protected override void OnLogicalFrame(object? sender, float dt)
    {
        if (_ePlayer == default)
        {
            return;
        }

        if (_ePlayer.Has<Transform3ToWorld>())
        {
            ref var cTransform3ToWorld = ref _ePlayer.Get<Transform3ToWorld>();
            _v3Player = cTransform3ToWorld.Matrix.Translation;
            
            
            Vector3 posShip = _v3Player;
            
            /*
             * Look up the zone we are in.
             */
            bool newZone = false;
            ClusterDesc foundCluster = I.Get<ClusterList>().GetClusterAt(posShip);
            if (foundCluster != null)
            {
                if (_currentCluster != foundCluster)
                {
                    /*
                     * We entered a new cluster. Trigger cluster song.
                     */
    
                    /*
                     * Remember new cluster.
                     */
                    _currentCluster = foundCluster;
                    newZone = true;
                }
            }
            else
            {
                if (_currentCluster != null)
                {
                    /*
                     * We just left a cluster. Trigger void music.
                     */
    
                    /*
                     * Remember we are outside.
                     */
                    _currentCluster = null;
                    newZone = true;
                }
            }
    

            if (newZone)
            {
                I.Get<EventQueue>().Push(
                    new ClusterWatchEvent(
                        ClusterWatchEvent.CLUSTER_CHANGED, "")
                    {
                        CurrentCluster = _currentCluster
                    });
            }
            
        }
    }

    
    protected override void OnModuleDeactivate()
    {
        _engine.Player.RemoveOnChange(_onPlayerEntityChanged);
    }


    protected override void OnModuleActivate()
    {
        _ePlayer = _engine.Player.Value;
        _engine.Player.AddNowOnChange(_onPlayerEntityChanged);
   }
}