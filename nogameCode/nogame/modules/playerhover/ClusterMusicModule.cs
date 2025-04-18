using System.Numerics;
using engine;
using engine.draw;
using engine.joyce.components;
using engine.world;

namespace nogame.modules.playerhover;

public class ClusterMusicModule : AModule
{
    /**
     * Display the current cluster name.
     */
    private DefaultEcs.Entity _eClusterDisplay;

    private ClusterDesc _currentCluster = null;

    private DefaultEcs.Entity _ePlayer;
    private Vector3 _v3Player;


    private string _getClusterSound(ClusterDesc clusterDesc)
    {
        if (null == clusterDesc)
        {
            return "lvl-6.ogg";
        }
        else
        {
            if (clusterDesc.Pos.Length() > 200)
            {
                return "lvl-1-01c.ogg";
            }
            else
            {
                return "shaklengokhsi.ogg";
            }
        }
    }


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
    
    
    private void _onLogicalFrame(object? sender, float dt)
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
    
            string displayName;
            if (_currentCluster != null)
            {
                displayName = $"{_currentCluster.Name}";
            }
            else
            {
                displayName = "void";
            }
    
            if (newZone)
            {
                if (_eClusterDisplay != default)
                {
                    _eClusterDisplay.Set(new engine.draw.components.OSDText(
                        new Vector2(768f / 2f - 64f - 48f - 96f, 48f),
                        new Vector2(96f, 18f),
                        $"{displayName}",
                        10,
                        0xff448822,
                        0x00000000,
                        HAlign.Right));
                }

                I.Get<Boom.Jukebox>().LoadThenPlaySong(
                    _getClusterSound(_currentCluster), 0.05f, true, () => { }, () => { });
            }
            
        }
    }

    
    public override void ModuleDeactivate()
    {
        _engine.OnLogicalFrame -= _onLogicalFrame;

        _engine.Player.RemoveOnChange(_onPlayerEntityChanged);

        var eClusterDisplay = _eClusterDisplay;
        _eClusterDisplay = default;
        _engine.QueueCleanupAction(() =>
        {
            eClusterDisplay.Disable();
            _engine.AddDoomedEntity(eClusterDisplay);
        });

       
        _engine.RemoveModule(this);

        base.ModuleDeactivate();
    }


    public override void ModuleActivate()
    {
        base.ModuleActivate();
        _engine.AddModule(this);

        _engine.Camera.AddOnChange(_onPlayerEntityChanged);


        _engine.QueueEntitySetupAction("OsdClusterDisplay", e => { _eClusterDisplay = e; });

        _engine.OnLogicalFrame += _onLogicalFrame;
    }

}