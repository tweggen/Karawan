using System;
using System.Collections.Generic;
using System.Numerics;
using engine;
using engine.draw;
using static engine.Logger;

namespace nogame.modules.daynite;


/**
 * We are responsible for the day night cycle but also for the
 * current in-game time.
 */
public class Module : AModule
{
    private DefaultEcs.Entity _eClockDisplay;
    
    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<AutoSave>()
    };


    public float RealSecondsPerGameDay { get; set; } = 30f * 60f;

    
    /**
     * We keep the start of the game in real world's time.
     *
     * We do know the logical start of the game, which is called GameState.GameT0
     */
    private DateTime _realWorldStart;
    
    
    /**
     * This is the start of the game in gane time. This is a constant we do not modify.
     */
    public DateTime GameStart { get; private set; }


    private DateTime _gameNow;
    
    /**
     * This is the current game time as in-game time.
     */
    public DateTime GameNow
    {
        get
        {
            if (!IsModuleActive())
            {
                ErrorThrow<InvalidOperationException>("Unable to read time if module (daynite.Module) is not started.");
            }
            lock (_lo)
            {
                return _gameNow;
            }
        }
        set
        {
            lock (_lo) 
            {
                if (!IsModuleActive())
                {
                    ErrorThrow<InvalidOperationException>("Unable to read time if module (daynite.Module) is not started.");
                }
                var sinceStartGameMilliSeconds = (value - GameStart).TotalMilliseconds;
                
                if (sinceStartGameMilliSeconds > 365*3600*24)
                {
                    int a = 1;
                }

                var sinceStartRealMilliSeconds = sinceStartGameMilliSeconds * RealSecondsPerGameDay / (24 * 60 * 60);
                _realWorldStart = DateTime.UtcNow - TimeSpan.FromMilliseconds(sinceStartRealMilliSeconds);
                if (_realWorldStart.Year > 2024)
                {
                    int a = 1;
                }
            }
        }
    }


    private void _onLogicalFrame(object? sender, float dt)
    {
        int todayGameHours, todayGameMinutes;
        
        /*
         * Remember the current in-game time. 
         */
        M<AutoSave>().GameState.GameNow = GameNow;

        
        /*
         * Then advance the current in-game time. 
         */
        lock (_lo)
        {
            var timeSinceStart = DateTime.UtcNow - _realWorldStart;
            float seconds = (float)timeSinceStart.TotalSeconds;
            float gameSeconds = (float)seconds / RealSecondsPerGameDay * 86400f;
            _gameNow = GameStart + TimeSpan.FromSeconds(gameSeconds);
            var tod = _gameNow.TimeOfDay;
            todayGameHours = tod.Hours;
            todayGameMinutes = tod.Minutes % 60;
        }

        if (_eClockDisplay.IsAlive)
        {
            _eClockDisplay.Set(new engine.draw.components.OSDText(
                new Vector2(768f/2f + 64f + 48f, 48f),
                new Vector2(128f, 16f),
                $"{todayGameHours:D2}:{todayGameMinutes:D2}",
                10,
                0xff448822,
                0x00000000,
                HAlign.Left
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
    

    public override void ModuleActivate()
    {
        base.ModuleActivate();
        _engine.AddModule(this);
        
        /*
         * Initialize the real world game start time with "now", which might or
         * might not be correct. Anyway, this represents the game starting in this very second.
         */
        _realWorldStart = DateTime.UtcNow;
        _eClockDisplay = _engine.CreateEntity("OsdClockDisplay");
        
        /*
         * Recall the current game time from the one in the save file.
         * This will modify _realWorldStart
         */
        GameNow = M<AutoSave>().GameState.GameNow;
        _engine.OnLogicalFrame += _onLogicalFrame;
    }

    public Module()
    {
        GameStart = GameState.GameT0;
    }
}