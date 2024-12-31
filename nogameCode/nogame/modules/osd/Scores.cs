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
        if (_engine.Player.TryGet(out var ePlayer) && ePlayer.Has<engine.physics.components.Body>())
        {
            lock (_engine.Simulation)
            {
                ref var prefPlayer = ref ePlayer.Get<engine.physics.components.Body>().Reference;
                var v3Vel = prefPlayer.Velocity.Linear with  { Y=0f };
                speed = (int)Single.Floor(v3Vel.Length() * 3.6f + 0.5f);
            }
        }

        {
            ref var cScoreOsdText = ref _eScoreDisplay.Get<engine.draw.components.OSDText>();
            cScoreOsdText.Text = $"{speed}";
            // cScoreOsdText.GaugeValue = (ushort)(4096f * speed / 200f);
        }
        {
            ref var cPolytopeOsdText = ref _ePolytopeDisplay.Get<engine.draw.components.OSDText>();
            cPolytopeOsdText.Text = $"{gameState.NumberPolytopes}";
        }
        {
            ref var cHealthOsdText = ref _eHealthDisplay.Get<engine.draw.components.OSDText>();
            cHealthOsdText.Text = $"{gameState.Health}";
        }
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
        _eScoreDisplay.Set(new engine.draw.components.OSDText(
            new Vector2(786f-64f-32f, 48f+YOffset),
            new Vector2(64f, 40f),
            $"0",
            32,
            0xff448822,
            0x00000000,
            HAlign.Right
        )
        {
            GaugeColor = 0x44448822,
            GaugeValue = 0
        });


        _ePolytopeDisplay = _engine.CreateEntity("OsdPolytopeDisplay");
        _ePolytopeDisplay.Set(new engine.behave.components.Clickable()
        {
            ClickEventFactory = (e, cev, v2RelPos) => new engine.news.Event("nogame.modules.menu.toggleMenu", null)
        });
        _ePolytopeDisplay.Set(new engine.draw.components.OSDText(
            new Vector2(786f-64f-32f-96f, 48f+YOffset),
            new Vector2(64f, 40f),
            $"",
            32,
            0xff448822,
            0x00000000,
            HAlign.Right
        ));


        _eHealthDisplay = _engine.CreateEntity("OsdHealthDisplay");
        _eHealthDisplay.Set(new engine.behave.components.Clickable()
        {
            ClickEventFactory = (e, cev, v2RelPos) => new engine.news.Event("nogame.modules.menu.toggleMenu", null)
        });
        _eHealthDisplay.Set(new engine.draw.components.OSDText(
            new Vector2(786f-64f-32f-48f, 48f+48f+YOffset),
            new Vector2(64f+48f, 40f),
            $"",
            32,
            0xff448822,
            0x00000000,
            HAlign.Right
        ));



        _engine.OnLogicalFrame += _onLogical;
    }
}