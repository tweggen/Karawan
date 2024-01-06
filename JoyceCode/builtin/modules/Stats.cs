using System.Numerics;
using engine;
using engine.draw;
using engine.news;

namespace builtin.modules;

public class Stats : engine.AModule
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


    private void _onLogicalFrame(object? sender, float dt)
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
                    var prefPlayer = _playerEntity.Get<engine.physics.components.Body>().Reference;
                    physData = $"pos: {prefPlayer.Pose.Position}, vel: {prefPlayer.Velocity.Linear}";
                }
            }

            displayData += physData + "\n";
        }
        
        _ePhysDisplay.Set(new engine.draw.components.OSDText(
            new Vector2(20f, 370f),
            new Vector2(400, 54),
            displayData,
            9,
            0xff22aaee,
            0x00000000,
            HAlign.Left
        ));
    }
    
    
    private void _onPlayerEntityChanged(object? sender, DefaultEcs.Entity entity)
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


    public override void ModuleDeactivate()
    {
        _engine.OnLogicalFrame -= _onLogicalFrame;
        _engine.OnPlayerEntityChanged -= _onPlayerEntityChanged;

        
        I.Get<SubscriptionManager>().Unsubscribe(
            Event.RENDER_STATS, _onRenderStats);

        _engine.RemoveModule(this);
        _ePhysDisplay.Dispose();

        base.ModuleDeactivate();
    }


    public override void ModuleActivate(engine.Engine engine0)
    {
        base.ModuleActivate(engine0);
        
        _ePhysDisplay = _engine.CreateEntity("OsdPhysDisplay");

        I.Get<SubscriptionManager>().Subscribe(
            Event.RENDER_STATS, _onRenderStats);

        _engine.AddModule(this);
        _onPlayerEntityChanged(_engine, _engine.GetPlayerEntity());
        _engine.OnPlayerEntityChanged += _onPlayerEntityChanged;
        _engine.OnLogicalFrame += _onLogicalFrame;
    }
}