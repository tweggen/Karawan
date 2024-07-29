using System;
using System.Collections.Generic;
using System.Net;
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


    private bool _syncOnline = false;
    private bool _isAutoSaveActive = false;

    public bool SyncOnline
    {
        get
        {
            lock (_lo)
            {
                return _syncOnline;
            }
        }
        set
        {
            if (_isAutoSaveActive)
            {
                ErrorThrow<ArgumentException>("Unable to activate online sync while an active game is going on.");
            }

            lock (_lo)
            {
                _syncOnline = value;
            }
        }
    }


    // public string GameServer { get; set; } = "https://silicondesert.io";
    public string GameServer { get; set; } = "http://localhost:4100";
    public int SaveInterval { get; set; } = 10;


    private GameState _gameState = null;

    public GameState GameState
    {
        get
        {
            lock (_lo)
            {
                if (!_isAutoSaveActive)
                {
                    ErrorThrow<InvalidOperationException>($"Unable to provide game state before start.");
                }

                return _gameState;
            }
        }

        private set { _gameState = value; }
    }



    public class LoginResult
    {
        public string token { get; set; } = "";
    };

    
    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<nogame.config.Module>(),
        new SharedModule<DBStorage>()
    };


    private void _performApiCall(string url, HttpContent content, string webToken, Action<HttpResponseMessage> onResponse)
    {
        if (content.Headers.Contains("x-nassau-token")) content.Headers.Remove("x-nassau-token");
        content.Headers.Add("x-nassau-token", webToken);
           
        I.Get<HttpClient>()
            
            .PostAsync(url, content)
            .ContinueWith(
                async (responseTask) =>
                {
                    var knownWebToken = M<nogame.config.Module>().GameConfig.WebToken;

                    HttpResponseMessage httpResponseMessage = await responseTask;
                    if (httpResponseMessage.StatusCode == HttpStatusCode.Forbidden) 
                    {
                        if (string.IsNullOrEmpty(knownWebToken))
                        {
                            Error($"Error while calling {url}: Call is unauthorized, even with a fresh token.");
                        }
                        else
                        {
                            M<nogame.config.Module>().GameConfig.WebToken = "";
                            M<nogame.config.Module>().Save();
                            Error($"Error while calling {url}: Call is unauthorized, using an existing token. Fetching new token.");
                            _withWebToken(url, content, onResponse);
                        }
                    }
                    else
                    {
                        if (httpResponseMessage.IsSuccessStatusCode)
                        {
                            Trace($"Done calling {url}");
                            if (knownWebToken != webToken)
                            {
                                Trace($"Saving web token.");
                                M<nogame.config.Module>().GameConfig.WebToken = webToken;
                                M<nogame.config.Module>().Save();
                            }

                            onResponse(httpResponseMessage);
                        }
                        else
                        {
                            string textResponse = await httpResponseMessage.Content.ReadAsStringAsync();
                            Trace($"Error while saving: {textResponse}");
                        }
                    }
                });
    }
    

    private void _withWebToken(string url, HttpContent content, Action<HttpResponseMessage> onResponse)
    {
        string webToken = M<nogame.config.Module>().GameConfig.WebToken;

        if (string.IsNullOrEmpty(webToken))
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
                        $"{GameServer}/api/public/users/log_in", 
                         JsonContent.Create(loginObject))
                    .ContinueWith(
                        async (responseTask) =>
                        {
                            var responseMessage = await responseTask;
                            string jsonResponse = await responseMessage.Content.ReadAsStringAsync();
                            
                            var objResponse = JsonSerializer.Deserialize<LoginResult>(jsonResponse);

                            if (objResponse.GetType() == typeof(LoginResult) && !string.IsNullOrEmpty(objResponse.token))
                            {
                                webToken = objResponse.token;
                                Trace($"GetToken response is {webToken}");
                                try
                                {
                                    _performApiCall(url, content, webToken, onResponse);
                                }
                                catch (UnauthorizedAccessException ex)
                                {
                                    Error($"Action {url} appears to be unauthorized, even with a fresh token.");
                                }
                            }
                            else
                            {
                                Trace($"GetToken error: {jsonResponse}");
                            }
                        });
            }
        }
        else
        {
            _performApiCall(url, content, webToken, onResponse);
        }
    }


    private async void _onSaveGameResponse(HttpResponseMessage response)
    {
        string textResponse = await response.Content.ReadAsStringAsync();
        Trace($"Save game text result {textResponse}.");
    }
    

    private void _triggerCloudSave(GameState gs)
    {
        var saveGameObject = new
        {
            game = new
            {
                title = "silicondesert2",
            },
            gamedata = JsonSerializer.Serialize(gs)
        };
        var httpContent = JsonContent.Create(saveGameObject);
        _withWebToken($"{GameServer}/api/auth/save_game", httpContent, _onSaveGameResponse);
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


    private void _stopAutoSave()
    {
        _saveTimer.Enabled = false;
    }
    
    
    private void _startAutoSave()
    {
        _saveTimer = new System.Timers.Timer(SaveInterval*1000);
        // Hook up the Elapsed event for the timer. 
        _saveTimer.Elapsed += _onSaveTimer;
        _saveTimer.AutoReset = true;
        _saveTimer.Enabled = true;
    }


    public void Save()
    {
        lock (_lo)
        {
            if (!_isAutoSaveActive)
            {
                Error($"Unable to save right now.");
                return;
            }
        }
        _doSave();
    }



    private void _triggerInitialOnlineLoad(Action<GameState> onInitialLoad)
    {
        
    }
    

    private void _triggerInitialOfflineLoad(Action<GameState> onInitialLoad)
    {
        bool haveGameState = M<DBStorage>().LoadGameState(out GameState gameState);
        if (false == haveGameState)
        {
            Trace($"Creating new gamestate");
            gameState = new GameState();
            M<DBStorage>().SaveGameState(gameState);
        }
        else
        {
            Trace($"Loading existing gamestate");
            if (!gameState.IsValid())
            {
                Trace($"... fixing existing gamestate");
                gameState.Fix();
            }
        }

        _gameState = gameState;
        _engine.QueueMainThreadAction(() =>
        {
            onInitialLoad(_gameState);
            _startAutoSave();
        });
    }
    

    private void _triggerInitialLoad(Action<GameState> onInitialLoad)
    {
        if (_syncOnline)
        {
            _triggerInitialOnlineLoad(onInitialLoad);
        }
        else
        {
            _triggerInitialOfflineLoad(onInitialLoad);
        }
    }
    
    

    public void StopAutoSave()
    {
        lock (_lo)
        {
            if (!_isAutoSaveActive)
            {
                return;
            }
            _isAutoSaveActive = false;
        }

        _stopAutoSave();
    }
    

    public void StartAutoSave(Action<GameState> onInitialLoad)
    {
        lock (_lo)
        {
            if (_isAutoSaveActive)
            {
                ErrorThrow<InvalidOperationException>($"Autosave already was active.");
            }
            _isAutoSaveActive = true;
        }

        _triggerInitialLoad(onInitialLoad);
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
    }
}