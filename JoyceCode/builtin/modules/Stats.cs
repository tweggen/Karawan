using System.Numerics;
using engine;
using engine.draw;
using engine.news;
using engine.world;

namespace builtin.modules;

public class Stats : engine.AController
{
    private object _lo = new();
    
    private DefaultEcs.Entity _ePhysDisplay;
    private DefaultEcs.Entity _playerEntity;
    
    private string _renderStats = "";

    private void _onRenderStats(Event ev)
    {
        lock (_lo)
        {
            _renderStats = ev.Code;
        }
    }


    protected override void OnLogicalFrame(object? sender, float dt)
    {
        string displayData = "";

        if (_renderStats != null || _renderStats != "")
        {
            displayData += _renderStats + "\n";
        }

        string physData = "";
        if (_playerEntity.IsAlive && _playerEntity.IsEnabled())
        {
            if (_playerEntity.Has<engine.physics.components.Body>())
            {
                lock (_engine.Simulation)
                {
                    ref var prefPlayer = ref _playerEntity.Get<engine.physics.components.Body>().Reference;
                    var v3Vel = prefPlayer.Velocity.Linear;
                    physData = $"pos: {prefPlayer.Pose.Position}, vel: {v3Vel.Length()} {v3Vel}";
                }
            }

            displayData += physData + "\n";
        }

        {
            var loader = I.Get<engine.world.MetaGen>().Loader;
            displayData += $"{loader.NFragments} frags, {loader.NViewers} viewers.";
        }
        
        _ePhysDisplay.Set(new engine.draw.components.OSDText(
            new Vector2(20f, 370f),
            new Vector2(400, 54),
            displayData,
            9,
            0x8822aaee,
            0x00000000,
            HAlign.Left
        ));
    }
    
    
    private void _onPlayerEntityChanged(DefaultEcs.Entity entity)
    {
        bool isChanged = false;
        lock (_lo)
        {
            if (_playerEntity != entity)
            {
                _playerEntity = entity;
                isChanged = true;
            }
        }

        if (isChanged)
        {
            /*
             * We do not update the AL listener, instead we assume the listener to be
             * at the origin. Instead, we wait for everything else to update.
             */
        }
    }


    protected override void OnModuleDeactivate()
    {
        _engine.Player.RemoveOnChange(_onPlayerEntityChanged);

        
        _ePhysDisplay.Dispose();
    }


    protected override void OnModuleActivate()
    {
        _ePhysDisplay = _engine.CreateEntity("OsdPhysDisplay");

        Subscribe(Event.RENDER_STATS, _onRenderStats);

        _engine.Player.AddNowOnChange(_onPlayerEntityChanged);
    }
}
