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
        var gameState = I.Get<GameState>();

        _eScoreDisplay.Set(new engine.draw.components.OSDText(
            new Vector2(786f-64f-32f, 48f+YOffset),
            new Vector2(64f, 40f),
            $"{gameState.NumberCubes}",
            32,
            0xff448822,
            0x00000000,
            HAlign.Right
        ));
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
    
    
    public override void ModuleActivate(Engine engine0)
    {
        base.ModuleActivate(engine0);
        _engine.AddModule(this);

        _eScoreDisplay = _engine.CreateEntity("OsdScoreDisplay");
        _ePolytopeDisplay = _engine.CreateEntity("OsdPolytopeDisplay");
        _eHealthDisplay = _engine.CreateEntity("OsdHealthDisplay");

        _engine.OnLogicalFrame += _onLogical;
    }
}