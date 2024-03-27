using System.Collections.Generic;
using System.Timers;
using engine;

namespace nogame.modules;


/**
 * Module to save the current game state.
 */
public class AutoSave : engine.AModule
{
    private System.Timers.Timer _saveTimer;
    
    
    public GameState GameState
    {
        get => I.Get<GameState>(); 
    }

    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<DBStorage>()
    };

    private void _onSaveTimer(object sender, ElapsedEventArgs e)
    {
        M<DBStorage>().SaveGameState(I.Get<GameState>());
    }


    private void _startAutoSave()
    {
        _saveTimer = new System.Timers.Timer(60000);
        // Hook up the Elapsed event for the timer. 
        _saveTimer.Elapsed += _onSaveTimer;
        _saveTimer.AutoReset = true;
        _saveTimer.Enabled = true;
    }
    

    public override void ModuleDeactivate()
    {
        _engine.RemoveModule(this);
        base.ModuleDeactivate();
    }
    

    public override void ModuleActivate()
    {
        base.ModuleActivate();
        _engine.AddModule(this);
        
        {
            bool haveGameState = M<DBStorage>().LoadGameState(out GameState gameState);
            if (false == haveGameState)
            {
                gameState = new GameState();
                M<DBStorage>().SaveGameState(gameState);
            }
            else
            {
                if (!gameState.IsValid())
                {
                    gameState.Fix();
                }
            }
            /*
             * Global Data structures
             */
            I.Register<GameState>(() => gameState);
        }
        
        _startAutoSave();
    }
}