using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Timers;
using engine;
using nogame.config;
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


    private string? _webToken = null;

    public class LoginResult
    {
        public string token { get; set; } = "";
    };

    
    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<nogame.config.Module>(),
        new SharedModule<DBStorage>()
    };


    private void _triggerSave(string webToken, GameState gs)
    {
        var content = JsonContent.Create(gs);
        content.Headers.Add("x-nassau-token", webToken);
           
        I.Get<HttpClient>()
            .PostAsync(
                //"http://localhost:4100/api/random", 
                "https://silicondesert.io/api/random", 
                content
                )
            .ContinueWith(
                async (responseTask) =>
                {
                    HttpResponseMessage httpResponseMessage = await responseTask;
                    string jsonResponse = await httpResponseMessage.Content.ReadAsStringAsync();
                    Trace($"Save response is {jsonResponse}");
                });
    }
    

    private void _withWebToken(Action<string> action)
    {
        string? webToken = null;
        lock (_lo)
        {
            webToken = _webToken;
        }

        if (null == webToken)
        {
            var gc = M<nogame.config.Module>().GameConfig;
            if (!string.IsNullOrEmpty(gc.Username) && !string.IsNullOrEmpty(gc.Password))
            {
                var loginObject = new
                {
                    user = new
                    {
                        email = gc.Username,
                        password = gc.Password
                    }
                };
                I.Get<HttpClient>()
                    .PostAsync(
                        "https://silicondesert.io/api/users/log_in",
                        //"http://localhost:4100/api/users/log_in",
                        JsonContent.Create(loginObject))
                    .ContinueWith(
                        async (responseTask) =>
                        {
                            var responseMessage = await responseTask;
                            string jsonResponse = await responseMessage.Content.ReadAsStringAsync();
                            
                            var objResponse = JsonSerializer.Deserialize<LoginResult>(jsonResponse);
                            webToken = objResponse.token;
                            lock (_lo)
                            {
                                _webToken = webToken;
                            }
                            action(webToken);
                            Trace($"GetToken response is {jsonResponse}");
                        });
            }
        }
        else
        {
            action(webToken);
        }
    }
     

    private void _triggerCloudSave(GameState gs)
    {
        _withWebToken(token => _triggerSave(token, gs));
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
        _saveTimer = new System.Timers.Timer(10000);
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