using System;
using System.Collections.Generic;
using System.Numerics;
using engine;
using engine.draw;

namespace nogame.modules.osd;


/**
 * Display content based on the game state.
 */
public class Scores : engine.AController
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


    protected override void OnLogicalFrame(object? sender, float dt)
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

            /*
             * KAR-483: on an intercity highway the speed shows in the boost colour, and the
             * gauge behind it fills with how much of the boost is on.
             */
            float boost = nogame.modules.playerhover.PlayerVehicleSpeed.Boost;
            bool boosted = boost > 1.05f;
            cScoreOsdText.TextColor = boosted ? 0xff22aaeeu : 0xff448822u;
            cScoreOsdText.GaugeColor = boosted ? 0x4422aaeeu : 0x44448822u;
            cScoreOsdText.GaugeValue = boosted
                ? (ushort)(4095f * Math.Clamp((boost - 1f) / (engine.world.IntercityCorridor.BoostFactor - 1f), 0f, 1f))
                : (ushort)0;
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
    
    
    protected override void OnModuleDeactivate()
    {
        _eScoreDisplay.Dispose();
        _ePolytopeDisplay.Dispose();
        _eHealthDisplay.Dispose();
    }
    
    
    protected override void OnModuleActivate()
    {
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
   }
}