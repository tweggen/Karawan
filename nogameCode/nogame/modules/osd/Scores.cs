using System;
using System.Collections.Generic;
using System.Numerics;
using engine;
using engine.draw;

namespace nogame.modules.osd;


/**
 * Display content based on the game state.
 */
public class Scores : engine.AModule
{
    public float YOffset { get; set; } = -5.0f;
    
    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<nogame.modules.AutoSave>()
    };


    /**
     * Display the current pling score.
     */
    private DefaultEcs.Entity _eScoreDisplay;
    
    /**
     * Display the current polytope score.
     */
    private DefaultEcs.Entity _ePolytopeDisplay;
    
    /**
     * Display the current polytope score.
     */
    private DefaultEcs.Entity _eHealthDisplay;


    private void _onLogical(object? sender, float dt)
    {
        var gameState = M<AutoSave>().GameState;

        int speed = 0;
        var ePlayer = _engine.GetPlayerEntity();
        if (ePlayer.IsAlive && ePlayer.IsEnabled() && ePlayer.Has<engine.physics.components.Body>())
        {
            lock (_engine.Simulation)
            {
                ref var prefPlayer = ref ePlayer.Get<engine.physics.components.Body>().Reference;
                var v3Vel = prefPlayer.Velocity.Linear;
                speed = (int)Single.Floor(v3Vel.Length() * 3.6f + 0.5f);
            }
        }
        
        _eScoreDisplay.Set(new engine.draw.components.OSDText(
            new Vector2(786f-64f-32f, 48f+YOffset),
            new Vector2(64f, 40f),
            $"{speed}",
            32,
            0xff448822,
            0x00000000,
            HAlign.Right
        )
        {
            GaugeColor = 0x44448822,
            GaugeValue = (ushort)(4096f * speed / 200f)
        });
        _ePolytopeDisplay.Set(new engine.draw.components.OSDText(
            new Vector2(786f-64f-32f-96f, 48f+YOffset),
            new Vector2(64f, 40f),
            $"{gameState.NumberPolytopes}",
            32,
            0xff448822,
            0x00000000,
            HAlign.Right
        ));
        _eHealthDisplay.Set(new engine.draw.components.OSDText(
            new Vector2(786f-64f-32f-48f, 48f+48f+YOffset),
            new Vector2(64f+48f, 40f),
            $"{gameState.Health}",
            32,
            0xff448822,
            0x00000000,
            HAlign.Right
        ));
    }
    
    
    public override void ModuleDeactivate()
    {
        _engine.RemoveModule(this);
        _engine.OnLogicalFrame -= _onLogical;
        
        _eScoreDisplay.Dispose();
        _ePolytopeDisplay.Dispose();
        _eHealthDisplay.Dispose();
        base.ModuleDeactivate();
    }
    
    
    public override void ModuleActivate()
    {
        base.ModuleActivate();
        _engine.AddModule(this);

        _eScoreDisplay = _engine.CreateEntity("OsdScoreDisplay");
        _eScoreDisplay.Set(new engine.behave.components.Clickable()
        {
            ClickEventFactory = (e, cev, v2RelPos) => new engine.news.Event("nogame.modules.menu.toggleMenu", null)
        });


        _ePolytopeDisplay = _engine.CreateEntity("OsdPolytopeDisplay");
        _ePolytopeDisplay.Set(new engine.behave.components.Clickable()
        {
            ClickEventFactory = (e, cev, v2RelPos) => new engine.news.Event("nogame.modules.menu.toggleMenu", null)
        });


        _eHealthDisplay = _engine.CreateEntity("OsdHealthDisplay");
        _eHealthDisplay.Set(new engine.behave.components.Clickable()
        {
            ClickEventFactory = (e, cev, v2RelPos) => new engine.news.Event("nogame.modules.menu.toggleMenu", null)
        });



        _engine.OnLogicalFrame += _onLogical;
    }
}