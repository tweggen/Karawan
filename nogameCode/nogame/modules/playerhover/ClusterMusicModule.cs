using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using builtin.modules;
using engine;
using engine.draw;
using engine.world;

namespace nogame.modules.playerhover;

public class ClusterMusicModule : AModule
{
    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<builtin.modules.ClusterWatch>()
    };
    
    /**
     * Display the current cluster name.
     */
    private DefaultEcs.Entity _eClusterDisplay;

    private ClusterDesc _currentCluster = null;

    
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


    private void _onClusterChanged(engine.news.Event ev)
    {
        ClusterWatchEvent cwe = ev as ClusterWatchEvent;
        Debug.Assert(cwe != null);

        if (_currentCluster != cwe.CurrentCluster)
        {
            _currentCluster = cwe.CurrentCluster;

            string displayName;
            if (_currentCluster != null)
            {
                displayName = $"{_currentCluster.Name}";
            }
            else
            {
                displayName = "void";
            }
    
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
    
    
    protected override void OnModuleDeactivate()
    {
        var eClusterDisplay = _eClusterDisplay;
        _eClusterDisplay = default;
        _engine.QueueCleanupAction(() =>
        {
            eClusterDisplay.Disable();
            _engine.AddDoomedEntity(eClusterDisplay);
        });
    }


    protected override void OnModuleActivate()
    {
        _engine.QueueEntitySetupAction("OsdClusterDisplay", e => { _eClusterDisplay = e; });
        
        Subscribe(ClusterWatchEvent.CLUSTER_CHANGED, _onClusterChanged);
   }

}