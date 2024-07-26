using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Timers;
using engine;
using nogame.config;
using ObjLoader.Loader.Common;
using static engine.Logger;

namespace nogame.modules;


/**
 * Module to save the current game state.
 */
public class AutoSave : engine.AModule
{
    private System.Timers.Timer _saveTimer;


    private GameState _gameState = null;
    public GameState GameState
    {
        get => _gameState;
    }

    
    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<nogame.config.Module>(),
        new SharedModule<DBStorage>()
    };


    private void _triggerCloudSave(GameState gs)
    {
        var jsonContent = JsonContent.Create(gs);

        var gc = M<nogame.config.Module>().GameConfig;
        if (!gc.Username.IsNullOrEmpty() && !gc.Password.IsNullOrEmpty())
        {
            I.Get<HttpClient>()
                .PostAsync("https://silicondesert.io/api/random", jsonContent)
                .ContinueWith(
                    async (responseTask) =>
                    {
                        var jsonResponse = await (await responseTask).Content.ReadAsStringAsync();
                        Trace($"Save response is {jsonResponse}");
                    });
        }
    }
    

    private void _doSave()
    {
        var gs = _gameState;
        
        M<DBStorage>().SaveGameState(gs);
        _triggerCloudSave(gs);
    }
    

    private void _onSaveTimer(object sender, ElapsedEventArgs e)
    {
        _doSave();
    }


    private void _startAutoSave()
    {
        _saveTimer = new System.Timers.Timer(60000);
        // Hook up the Elapsed event for the timer. 
        _saveTimer.Elapsed += _onSaveTimer;
        _saveTimer.AutoReset = true;
        _saveTimer.Enabled = true;
    }


    public void Save()
    {
        _doSave();
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

            _gameState = gameState;
        }
        
        _startAutoSave();
    }
}