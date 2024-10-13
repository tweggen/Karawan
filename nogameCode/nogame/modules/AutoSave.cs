using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
using engine;
using engine.draw;
using engine.draw.components;
using engine.news;
using static engine.Logger;

namespace nogame.modules;


/**
 * Module to save the current game state.
 */
public class AutoSave : engine.AModule
{
    private System.Timers.Timer _saveTimer;


    public float YOffset { get; set; } = -5.0f;

    private bool _syncOnline = false;
    private bool _isAutoSaveActive = false;


    private DefaultEcs.Entity _eSaveOnlineDisplay; 
    
    
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


#if true
    public string GameServer { get; set; } = "https://silicondesert.io";
    public int SaveInterval { get; set; } = 60;
#else
    public string GameServer { get; set; } = "http://localhost:4100";
    public int SaveInterval { get; set; } = 10;
#endif


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


    public class SaveGame
    {
        public string gamedata { get; set; }
        public string storedAt { get; set; }
    }

    
    public class SaveGameGetResult
    {
        public SaveGame save { get; set; }
    }

    
    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<Saver>(),
        new SharedModule<nogame.config.Module>(),
        new SharedModule<DBStorage>()
    };


    private void _updateOSDText()
    {
        string strStatus = "";

        lock (_lo)
        {
            if (_isLoggedIn)
            {
                strStatus += "logged in";
            }
            
            if (_isSaving)
            {
                if (strStatus != "") strStatus += ", ";
                strStatus += "saving...";
            }
        }

        _eSaveOnlineDisplay.Get<OSDText>().Text = strStatus;
    }
    
    
    private void _onLoggedIn(bool isLoggedIn)
    {
        _updateOSDText();
    }


    private bool _isSaving = false;

    private void _setSaving(bool isSaving)
    {
        lock (_lo)
        {
            if (_isSaving == isSaving)
            {
                return;
            }

            _isSaving = isSaving;
        }

        _updateOSDText();
    }
    
    
    private bool _isLoggedIn = false;
    
    private void _setLoggedIn(bool isLoggedIn)
    {
        lock (_lo)
        {
            if (_isLoggedIn == isLoggedIn)
            {
                return;
            }
            _isLoggedIn = isLoggedIn;
        }
        
        I.Get<EventQueue>().Push(new Event("nogame.module.AutoSave.IsOnline", $"{isLoggedIn}"));

        _onLoggedIn(isLoggedIn);
    }
    

    private void _performApiCall(Func<HttpRequestMessage> httpRequestMessageFunc, string webToken, Action<HttpResponseMessage> onResponse)
    {
        HttpRequestMessage httpRequestMessage = httpRequestMessageFunc();
        if (httpRequestMessage.Headers.Contains("x-nassau-token"))
        {
            httpRequestMessage.Headers.Remove("x-nassau-token");
        }
        httpRequestMessage.Headers.Add("x-nassau-token", webToken);
           
        I.Get<HttpClient>()
            
            .SendAsync(httpRequestMessage)
            .ContinueWith(
                async (responseTask) =>
                {
                    var knownWebToken = M<nogame.config.Module>().GameConfig.WebToken;

                    HttpResponseMessage httpResponseMessage = await responseTask;
                    if (httpResponseMessage.StatusCode == HttpStatusCode.Forbidden || httpResponseMessage.StatusCode == HttpStatusCode.Unauthorized) 
                    {
                        if (string.IsNullOrEmpty(knownWebToken))
                        {
                            Error($"Error while calling {httpRequestMessage.RequestUri}: Call is unauthorized, even with a fresh token.");
                        }
                        else
                        {
                            _setLoggedIn(false);
                            M<nogame.config.Module>().GameConfig.WebToken = "";
                            M<nogame.config.Module>().Save();
                            Error($"Error while calling {httpRequestMessage.RequestUri}: Call is unauthorized, using an existing token. Fetching new token.");
                            _withWebToken(httpRequestMessageFunc, onResponse, 5);
                        }
                    }
                    else
                    {
                        Trace($"Saving web token.");
                        M<nogame.config.Module>().GameConfig.WebToken = webToken;
                        M<nogame.config.Module>().Save();
                        _setLoggedIn(true);
         
                        if (httpResponseMessage.IsSuccessStatusCode)
                        {
                            Trace($"Done calling {httpRequestMessage.RequestUri}");

                            onResponse(httpResponseMessage);
                        }
                        else 
                        {
                            string textResponse = await httpResponseMessage.Content.ReadAsStringAsync();
                            Trace($"Error while saving: {textResponse}");
                            
                            onResponse(httpResponseMessage);
                        }
                    }
                });
    }
    

    private void _withWebToken(
        Func<HttpRequestMessage> httpRequestMessageFunc,
        Action<HttpResponseMessage> onResponse,
        int nRetries
    )
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
                                    _performApiCall(httpRequestMessageFunc, webToken, onResponse);
                                }
                                catch (UnauthorizedAccessException ex)
                                {
                                    Error($"performApiCall raised an UnautorizedAccessException: {ex}");
                                }
                                catch (Exception ex)
                                {
                                    Error($"Error calling api: {ex}");
                                }
                            }
                            else
                            {
                                if (nRetries > 0)
                                {
                                    Trace($"GetToken error, retrying ({nRetries} retries left): {jsonResponse}");
                                    Task.Delay(2000)
                                        .ContinueWith(t => _withWebToken(httpRequestMessageFunc, onResponse, nRetries - 1));
                                } 
                                else
                                {
                                    onResponse(responseMessage);
                                    Trace($"GetToken error.");
                                }
                            }
                        });
            }
        }
        else
        {
            try
            {
                _performApiCall(httpRequestMessageFunc, webToken, onResponse);
            }
            catch (Exception ex)
            {
                Error($"Error calling api: {ex}");
            }
        }
    }


    private async void _onSaveGameResponse(HttpResponseMessage response)
    {
        string textResponse = await response.Content.ReadAsStringAsync();
        Trace($"Save game text result {textResponse}.");
        _setSaving(false);
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

        Trace($"Save game data is {saveGameObject.gamedata}");

        _withWebToken(() =>
        {
            
            return new HttpRequestMessage(
                HttpMethod.Post,
                $"{GameServer}/api/auth/save_game")
            {
                Content = JsonContent.Create(saveGameObject)
            };
        }, _onSaveGameResponse, 5);
    }
    

    private void _doSave()
    {
        _setSaving(true);
        var gs = _gameState;
        M<DBStorage>().SaveGameState(gs);
        
        _triggerCloudSave(gs);
    }
    

    private void _onSaveTimer(object sender, ElapsedEventArgs e)
    {
        M<Saver>().Save("auto save");
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


    private void _save()
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


    private void _afterLoad(GameState gs)
    {
        M<Saver>().CallAfterLoad(gs);
    }
    

    private void _loadCreateOffline()
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

        _afterLoad(gameState);
    }


    private async void _onGameHistoryResponse(
        HttpResponseMessage httpResponseMessage)
    {
        if (httpResponseMessage.IsSuccessStatusCode)
        {
            string strSaves = await httpResponseMessage.Content.ReadAsStringAsync();
            Trace($"Saves response is {strSaves}");
        }
    }


    private async void _onLoadGameResponse(
        HttpResponseMessage httpResponseMessage,
        Action<GameState> onInitialLoad)
    {
        bool haveGameState = false;
        if (httpResponseMessage.IsSuccessStatusCode)
        {
            string strSave = await httpResponseMessage.Content.ReadAsStringAsync();
            Trace($"Load game response is {strSave}");
            var save = JsonSerializer.Deserialize<SaveGameGetResult>(strSave);
            var gs = JsonSerializer.Deserialize<GameState>(save.save.gamedata);
            if (null != gs)
            {
                if (!gs.IsValid())
                {
                    gs.Fix();
                }

                _gameState = gs;
                haveGameState = true;
                _afterLoad(gs);
            }
        }

        if (!haveGameState)
        {
            Trace($"Using fallback to local gamestate");
            _loadCreateOffline();
        }
        
        _startAutoSave();
        onInitialLoad(_gameState);
    }
    
    
    /**
     * Try to login and load a previous save game.
     * No previous save game could be found, the offline game
     * state is loaded or a new one is created.
     */
    private void _triggerInitialOnlineLoad(bool reset, Action<GameState> onInitialLoad)
    {
        if (!reset)
        {
            _withWebToken(() =>
            {
                return new HttpRequestMessage(
                    HttpMethod.Get,
                    $"{GameServer}/api/auth/save_game?gameTitle=silicondesert2");
            }, (response) => _onLoadGameResponse(response, onInitialLoad), 5);
            

            #if false
            /*
             * As a one time debugging: Display history.
             */
            _withWebToken(() =>
            {
                return new HttpRequestMessage(
                    HttpMethod.Get,
                    $"{GameServer}/api/auth/saves?gameTitle=silicondesert2");
            }, (response) => _onGameHistoryResponse(response), 5);
            #endif

        }
        else
        {
            Trace($"Reset game.");
            var gameState = new GameState();
            M<DBStorage>().SaveGameState(gameState);
            _gameState = gameState;
            _doSave();
            _engine.Run(() =>
            {
                onInitialLoad(gameState);
            });
            
        }
    }
    

    private void _triggerInitialOfflineLoad(Action<GameState> onInitialLoad)
    {
        _loadCreateOffline();
        _engine.QueueMainThreadAction(() =>
        {
            onInitialLoad(_gameState);
            _startAutoSave();
        });
    }
    

    private void _triggerInitialLoad(bool reset, Action<GameState> onInitialLoad)
    {
        if (_syncOnline)
        {
            _triggerInitialOnlineLoad(reset, onInitialLoad);
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
    

    public void StartAutoSave(bool reset, Action<GameState> onInitialLoad)
    {
        lock (_lo)
        {
            if (_isAutoSaveActive)
            {
                ErrorThrow<InvalidOperationException>($"Autosave already was active.");
            }
            _isAutoSaveActive = true;
        }

        _triggerInitialLoad(reset, onInitialLoad);
    }
    

    private void _handleTriggerSave()
    {
        _save();
    }


    public override void ModuleDeactivate()
    {
        M<Saver>().SaveAction = default;        
        _engine.RemoveModule(this);
        base.ModuleDeactivate();
    }
    

    public override void ModuleActivate()
    {
        base.ModuleActivate();
        _engine.AddModule(this);

        M<Saver>().SaveAction = _handleTriggerSave;        
        
        _eSaveOnlineDisplay = _engine.CreateEntity("SaveOnlineDisplay");
        // _eSaveOnlineDisplay.Set(new engine.behave.components.Clickable()
        // {
        //    ClickEventFactory = (e, cev, v2RelPos) => new engine.news.Event("nogame.modules.menu.toggleMenu", null)
        // });
        _eSaveOnlineDisplay.Set(new engine.draw.components.OSDText(
            new Vector2(786f-64f-32f-48f, 48+48f+48f+YOffset),
            new Vector2(64f+48f, 40f),
            $"",
            12,
            0xff448822,
            0x00000000,
            HAlign.Right
        ));
    }
}