using System;
using System.Collections.ObjectModel;
using System.Numerics;
using engine;
using engine.draw;

namespace nogame.modules.daynite;

public class Module : AModule
{
    private DefaultEcs.Entity _eClockDisplay;
    
    public float RealSecondsPerDay { get; set; } = 30f * 60f;

    private DateTime _realWorldStart;
    public DateTime GameStart { get; set; } = new DateTime(1982, 3, 12, 10, 33, 0);
    
    private DateTime _gameNow;
    public DateTime GameNow
    {
        get
        {
            lock (_lo)
            {
                return _gameNow;
            }
        }
    }


    private void _onLogicalFrame(object? sender, float dt)
    {
        int todayGameHours, todayGameMinutes;
        
        lock (_lo)
        {
            var timeSinceStart = DateTime.UtcNow - _realWorldStart;
            float seconds = (float)timeSinceStart.TotalSeconds;
            float gameSeconds = (float)seconds / RealSecondsPerDay * 86400f;
            _gameNow = GameStart + TimeSpan.FromSeconds(gameSeconds);
            var tod = _gameNow.TimeOfDay;
            todayGameHours = tod.Hours;
            todayGameMinutes = tod.Minutes % 60;
        }

        if (_eClockDisplay.IsAlive)
        {
            _eClockDisplay.Set(new engine.draw.components.OSDText(
                new Vector2((768 + 144f) / 2f, 48f),
                new Vector2(128f, 16f),
                $"{todayGameHours:D2}:{todayGameMinutes:D2}",
                10,
                0xff448822,
                0x00000000,
                HAlign.Center
            ));
        }
    }


    public override void ModuleDeactivate()
    {
        _engine.OnLogicalFrame -= _onLogicalFrame;
        _eClockDisplay.Dispose();
        _engine.RemoveModule(this);
        base.ModuleDeactivate();
    }
    

    public override void ModuleActivate(Engine engine0)
    {
        base.ModuleActivate(engine0);
        _engine.AddModule(this);
        _realWorldStart = DateTime.UtcNow;
        _eClockDisplay = _engine.CreateEntity("OsdClockDisplay");
        _engine.OnLogicalFrame += _onLogicalFrame;
    }
}