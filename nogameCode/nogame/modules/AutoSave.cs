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
    private bool _isInitialLoadPending = false;


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


    public string GameServer { get; set; } = "https://silicondesert.io";
    public int SaveInterval { get; set; } = 60;

    /**
     * Upper bounds for one server call. The default HttpClient timeout
     * of 100s left the login screen looking dead for well over a minute.
     */
    public TimeSpan LoginTimeout { get; set; } = TimeSpan.FromSeconds(15);
    public TimeSpan ApiTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /**
     * Retries of a transient failure (no answer, 5xx) while the player
     * waits on the login screen; background saves keep retrying longer.
     */
    public int InitialLoginRetries { get; set; } = 2;

    

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
        new SharedModule<DBStorage>(),
        new MyModule<builtin.EntitySaver>()
    };


    private void _updateOSDText()
    {
        string strStatus = "";

        lock (_lo)
        {
            if (_isAuthenticating)
            {
                strStatus += "authenticating...";
            }
            else
            {
                if (_isLoggedIn)
                {
                    strStatus += "logged in";
                }
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
    
    
    private bool _isAuthenticating = false;
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
    

    private void _setAuthenticating(bool isAuthenticating)
    {
        Trace($"Called _setAuthenticating with {isAuthenticating}");
        lock (_lo)
        {
            if (_isAuthenticating == isAuthenticating)
            {
                return;
            }

            Trace($"Setting isAuthenticating to {isAuthenticating}");
            _isAuthenticating = isAuthenticating;
        }

        _updateOSDText();
    }
    
    
    /**
     * Run an async server interaction without losing its faults: whatever
     * escapes the body is handed to onException instead of vanishing in an
     * unobserved task.
     */
    private static void _runGuarded(Func<Task> body, Action<Exception> onException)
    {
        Task.Run(async () =>
        {
            try
            {
                await body();
            }
            catch (Exception e)
            {
                try
                {
                    onException(e);
                }
                catch (Exception eHandler)
                {
                    Error($"Exception while handling server call failure {e}: {eHandler}");
                }
            }
        });
    }


    private static bool _isTransient(Exception e) =>
        e is HttpRequestException or OperationCanceledException
        || e is AggregateException { InnerException: HttpRequestException or OperationCanceledException };


    private void _performApiCall(Func<HttpRequestMessage> httpRequestMessageFunc, string webToken, Action<HttpResponseMessage> onResponse, Action<string> onError, int nRetries)
    {
        _runGuarded(async () =>
        {
            HttpRequestMessage httpRequestMessage = httpRequestMessageFunc();
            if (httpRequestMessage.Headers.Contains("x-nassau-token"))
            {
                httpRequestMessage.Headers.Remove("x-nassau-token");
            }
            httpRequestMessage.Headers.Add("x-nassau-token", webToken);

            var knownWebToken = M<nogame.config.Module>().GameConfig.WebToken;

            HttpResponseMessage httpResponseMessage;
            using (var cts = new System.Threading.CancellationTokenSource(ApiTimeout))
            {
                httpResponseMessage = await I.Get<HttpClient>().SendAsync(httpRequestMessage, cts.Token);
            }

            if (httpResponseMessage.StatusCode == HttpStatusCode.Forbidden || httpResponseMessage.StatusCode == HttpStatusCode.Unauthorized) 
            {
                if (string.IsNullOrEmpty(knownWebToken))
                {
                    _setAuthenticating(false);
                    string textResponse = await httpResponseMessage.Content.ReadAsStringAsync();
                    Error($"Error while calling {httpRequestMessage.RequestUri}: Call is unauthorized, even with a fresh token.");
                    onError($"access refused: {ServerErrorText.Describe(httpResponseMessage.StatusCode, httpResponseMessage.ReasonPhrase, textResponse)}");
                }
                else
                {
                    _setLoggedIn(false);
                    _setAuthenticating(false);
                    M<nogame.config.Module>().GameConfig.WebToken = "";
                    M<nogame.config.Module>().Save();
                    Error($"Error while calling {httpRequestMessage.RequestUri}: Call is unauthorized, using an existing token. Fetching new token.");
                    _withWebToken(httpRequestMessageFunc, onResponse, onError, nRetries);
                }
            }
            else
            {
                _setAuthenticating(false);
                Trace($"Saving web token.");
                M<nogame.config.Module>().GameConfig.WebToken = webToken;
                M<nogame.config.Module>().Save();
                _setLoggedIn(true);
     
                if (httpResponseMessage.IsSuccessStatusCode)
                {
                    Trace($"Done calling {httpRequestMessage.RequestUri}");
                }
                else 
                {
                    string textResponse = await httpResponseMessage.Content.ReadAsStringAsync();
                    Trace($"Error while saving: {textResponse}");
                }

                onResponse(httpResponseMessage);
            }
        }, e =>
        {
            _setAuthenticating(false);
            Warning($"Server call failed: {e}");
            onError(ServerErrorText.Describe(e));
        });
    }
    

    /**
     * Call the server with a web token, logging in first if we have none.
     *
     * Every way this can fail ends in exactly one onError call: missing
     * credentials, a login the server rejects (with the server's message),
     * an unreachable server or a timeout. Only transient failures (no
     * answer, 5xx) are retried; a rejected login is not.
     */
    private void _withWebToken(
        Func<HttpRequestMessage> httpRequestMessageFunc,
        Action<HttpResponseMessage> onResponse,
        Action<string> onError,
        int nRetries
    )
    {
        string webToken = M<nogame.config.Module>().GameConfig.WebToken;

        if (!string.IsNullOrEmpty(webToken))
        {
            _setAuthenticating(false);
            _performApiCall(httpRequestMessageFunc, webToken, onResponse, onError, nRetries);
            return;
        }

        var gc = M<nogame.config.Module>().GameConfig;
        if (string.IsNullOrEmpty(gc.Username) || string.IsNullOrEmpty(gc.Password))
        {
            _setAuthenticating(false);
            onError("enter eMail and password to log in");
            return;
        }

        _setAuthenticating(true);
        var loginObject = new
        {
            user = new
            {
                email = gc.Username,
                password = gc.Password
            }
        };

        void retryOrFail(string reason)
        {
            if (nRetries > 0)
            {
                Warning($"Login attempt failed ({reason}), retrying, {nRetries} retries left.");
                Task.Delay(2000)
                    .ContinueWith(t => _withWebToken(httpRequestMessageFunc, onResponse, onError, nRetries - 1));
            }
            else
            {
                _setAuthenticating(false);
                onError($"login failed: {reason}");
            }
        }

        _runGuarded(async () =>
        {
            HttpResponseMessage responseMessage;
            string jsonResponse;
            using (var cts = new System.Threading.CancellationTokenSource(LoginTimeout))
            {
                responseMessage = await I.Get<HttpClient>().PostAsync(
                    $"{GameServer}/api/public/users/log_in",
                    JsonContent.Create(loginObject),
                    cts.Token);
                jsonResponse = await responseMessage.Content.ReadAsStringAsync(cts.Token);
            }

            if (responseMessage.IsSuccessStatusCode)
            {
                LoginResult objResponse = null;
                try
                {
                    objResponse = JsonSerializer.Deserialize<LoginResult>(jsonResponse);
                }
                catch (JsonException)
                {
                }

                if (!string.IsNullOrEmpty(objResponse?.token))
                {
                    _setAuthenticating(false);
                    _performApiCall(httpRequestMessageFunc, objResponse.token, onResponse, onError, nRetries);
                    return;
                }
            }

            string reason = ServerErrorText.Describe(responseMessage.StatusCode, responseMessage.ReasonPhrase, jsonResponse);
            if ((int)responseMessage.StatusCode >= 500)
            {
                retryOrFail(reason);
            }
            else
            {
                _setAuthenticating(false);
                onError($"login failed: {reason}");
            }
        }, e =>
        {
            if (_isTransient(e))
            {
                retryOrFail(ServerErrorText.Describe(e));
            }
            else
            {
                _setAuthenticating(false);
                Warning($"Login failed: {e}");
                onError($"login failed: {ServerErrorText.Describe(e)}");
            }
        });
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

//        Trace($"Save game data is {saveGameObject.gamedata}");

        _withWebToken(() =>
        {
            
            return new HttpRequestMessage(
                HttpMethod.Post,
                $"{GameServer}/api/auth/save_game")
            {
                Content = JsonContent.Create(saveGameObject)
            };
        }, _onSaveGameResponse, (reason) =>
        {
            Warning($"Cloud save failed: {reason}");
            _setSaving(false);
        }, 5);
    }
    

    /**
     * async void: a fault escaping here would kill the process, so it is
     * caught and logged.
     */
    private async void _doSave()
    {
        try
        {
            _setSaving(true);

            var jnAllEntities = await M<builtin.EntitySaver>().SaveAll();

            var gs = _gameState;
            gs.Entities = JsonSerializer.Serialize(
                jnAllEntities,
                new JsonSerializerOptions()
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                }
            );

            //Trace($"Saving entities json: {gs.Entities}");
            M<DBStorage>().SaveGameState(gs);

            _triggerCloudSave(gs);
        }
        catch (Exception e)
        {
            Error($"Unable to save the game: {e}");
            _setSaving(false);
        }
    }
    

    private void _onSaveTimer(object sender, ElapsedEventArgs e)
    {
        M<Saver>().Save("auto save");
    }


    private void _stopAutoSave()
    {
        if (null != _saveTimer)
        {
            _saveTimer.Enabled = false;
        }
    }
    
    
    private void _startAutoSave()
    {
        _saveTimer = new System.Timers.Timer(SaveInterval*1000);
        // Hook up the Elapsed event for the timer. 
        _saveTimer.Elapsed += _onSaveTimer;
        _saveTimer.AutoReset = true;
        _saveTimer.Enabled = true;
    }


    /**
     * Must be called in logical thread.
     */
    private async Task _restoreEntityAndConfigFromLoad(GameState gs)
    {
        try
        {
            JsonDocument jdocGameState = JsonDocument.Parse(gs.Entities, 
                new()
                {
                    AllowTrailingCommas = true
                });
            var jeRoot = jdocGameState.RootElement;

            await M<builtin.EntitySaver>().LoadAll(jeRoot);
        }
        catch (Exception e)
        {
            Error($"Unable to restore entites from game state: {e}.");
        }

        _afterCreateOrLoad(gs);
    }
    

    /**
     * Called as a fallback if no online state could be found
     * or if the game was started new.
     * This can run in any thread.
     */
    private async Task _loadCreateOffline()
    {
        bool haveGameState = M<DBStorage>().LoadGameState(out GameState gameState);
        if (false == haveGameState)
        {
            Trace($"Creating new gamestate");
            gameState = new GameState();
            
            /*
             * Well, this is supposed to be started from just about anywhere, as it might want
             * to use logical thread operations.
             * TXWTODO: How to wait for it?
             */
            await M<Saver>().CallOnCreateNewGame(gameState)();
            
            _gameState = gameState;

            /*
             * After everything has been initialized, fire off the save of the initial game state.
             */
            _doSave();
            
            _afterCreate(gameState);
        }
        else
        {
            Trace($"Loading existing gamestate");
            if (!gameState.IsValid())
            {
                Trace($"... fixing existing gamestate");
                gameState.Fix();
            }

            _gameState = gameState;

            await _restoreEntityAndConfigFromLoad(gameState);
            
        }

        /*
         * Caller will call on initial load and start autosave.
         */
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


    /**
     * The login worked. An account without an online save (no save in
     * the answer, or 404) continues with the local game state; any other
     * failure is reported, never silently replaced by the local game.
     */
    private async Task _onLoadGameResponse(
        HttpResponseMessage httpResponseMessage,
        Action<GameState> onInitialLoad,
        Action<string> onError)
    {
        try
        {
            bool haveGameState = false;
            if (httpResponseMessage.IsSuccessStatusCode)
            {
                string strSave = await httpResponseMessage.Content.ReadAsStringAsync();
                // Trace($"Load game response is {strSave}");
                var save = JsonSerializer.Deserialize<SaveGameGetResult>(strSave);
                if (!string.IsNullOrEmpty(save?.save?.gamedata))
                {
                    var gs = JsonSerializer.Deserialize<GameState>(save.save.gamedata);
                    if (null != gs)
                    {
                        if (!gs.IsValid())
                        {
                            gs.Fix();
                        }

                        _gameState = gs;
                        haveGameState = true;

                        await _restoreEntityAndConfigFromLoad(gs);
                    }
                }
            }
            else if (httpResponseMessage.StatusCode != HttpStatusCode.NotFound)
            {
                string textResponse = await httpResponseMessage.Content.ReadAsStringAsync();
                onError($"online save: {ServerErrorText.Describe(httpResponseMessage.StatusCode, httpResponseMessage.ReasonPhrase, textResponse)}");
                return;
            }

            if (!haveGameState)
            {
                Trace($"Using fallback to local gamestate");
                await _loadCreateOffline();
            }
        }
        catch (Exception e)
        {
            Warning($"Unable to load the online save: {e}");
            onError($"online save: {ServerErrorText.Describe(e)}");
            return;
        }

        await _engine.TaskMainThread(() =>
        {
            onInitialLoad(_gameState);
        });
        
        _startAutoSave();
    }


    private void _afterCreateOrLoad(GameState gs)
    {
        M<Saver>().CallAfterLoad(gs);
    }
    
    
    private void _afterCreate(GameState gs)
    {
        _afterCreateOrLoad(gs);
    }


    private void _createNew()
    {
        
    }
    
    
    /**
     * Try to login and load a previous save game.
     * No previous save game could be found, the offline game
     * state is loaded or a new one is created.
     */
    private void _triggerInitialOnlineLoad(bool reset, Action<GameState> onInitialLoad, Action<string> onError)
    {
        if (!reset)
        {
            _withWebToken(() =>
            {
                return new HttpRequestMessage(
                    HttpMethod.Get,
                    $"{GameServer}/api/auth/save_game?gameTitle=silicondesert2");
            }, (response) => _onLoadGameResponse(response, onInitialLoad, onError), 
                onError,
                InitialLoginRetries);
        }
        else
        {
            /*
             * "new (globally)" destroys the online save, so make sure the
             * login works before creating anything: fetching the old save
             * is the cheapest call that proves the credentials.
             */
            _withWebToken(() =>
                {
                    return new HttpRequestMessage(
                        HttpMethod.Get,
                        $"{GameServer}/api/auth/save_game?gameTitle=silicondesert2");
                }, (response) =>
                {
                    if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
                    {
                        _ = _createNewOnline(onInitialLoad, onError);
                    }
                    else
                    {
                        onError($"server not ready: {ServerErrorText.Describe(response.StatusCode, response.ReasonPhrase, null)}");
                    }
                },
                onError,
                InitialLoginRetries);
        }
    }


    private async Task _createNewOnline(Action<GameState> onInitialLoad, Action<string> onError)
    {
        try
        {
            Trace($"Reset game.");
            var gameState = new GameState();

            /*
             * Well, this is supposed to be started from just about anywhere, as it might want
             * to use logical thread operations.
             * TXWTODO: How to wait for it?
             */
            await M<Saver>().CallOnCreateNewGame(gameState)();

            _gameState = gameState;

            /*
             * After everything has been initialized, fire off the save of the initial game state.
             */
            _doSave();

            _afterCreate(gameState);
        }
        catch (Exception e)
        {
            Warning($"Unable to create a new game: {e}");
            onError($"new game: {ServerErrorText.Describe(e)}");
            return;
        }

        await _engine.TaskMainThread(() =>
        {
            onInitialLoad(_gameState);
        });

        _startAutoSave();
    }
    

    private async Task _triggerInitialOfflineLoad(Action<GameState> onInitialLoad, Action<string> onError)
    {
        try
        {
            await _loadCreateOffline();
        }
        catch (Exception e)
        {
            Warning($"Unable to load the local game: {e}");
            onError($"local game: {ServerErrorText.Describe(e)}");
            return;
        }
        
        await _engine.TaskMainThread(() =>
        {
            onInitialLoad(_gameState);
        });
        
        _startAutoSave();
    }
    

    private void _triggerInitialLoad(bool reset, Action<GameState> onInitialLoad, Action<string> onError)
    {
        if (_syncOnline)
        {
            _triggerInitialOnlineLoad(reset, onInitialLoad, onError);
        }
        else
        { 
            _ = _triggerInitialOfflineLoad(onInitialLoad, onError);
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
    

    /**
     * Load or create the game state, then start saving periodically.
     *
     * Exactly one of onInitialLoad (on the logical thread) or onError (on
     * any thread) is called. After onError, autosave is inactive again, so
     * the caller may change SyncOnline and call StartAutoSave once more.
     */
    public void StartAutoSave(bool reset, Action<GameState> onInitialLoad, Action<string> onError)
    {
        lock (_lo)
        {
            if (_isAutoSaveActive)
            {
                ErrorThrow<InvalidOperationException>($"Autosave already was active.");
            }
            _isAutoSaveActive = true;
            _isInitialLoadPending = true;
        }

        void onLoaded(GameState gs)
        {
            lock (_lo)
            {
                if (!_isInitialLoadPending) return;
                _isInitialLoadPending = false;
            }

            /*
             * This runs inside TaskMainThread, whose task never completes if
             * the action throws; the caller owns reporting its own failures.
             */
            try
            {
                onInitialLoad(gs);
            }
            catch (Exception e)
            {
                Error($"Exception in the initial load callback: {e}");
            }
        }

        void onFailed(string reason)
        {
            lock (_lo)
            {
                if (!_isInitialLoadPending) return;
                _isInitialLoadPending = false;
                _isAutoSaveActive = false;
            }
            Warning($"Initial load failed: {reason}");
            onError(reason);
        }
        
        _triggerInitialLoad(reset, onLoaded, onFailed);
    }
    

    private void _handleTriggerSave()
    {
        /*
         * This is just a trigger. We can very well trigger an async call.
         */
        _doSave();
    }


    protected override void OnModuleDeactivate()
    {
        _eSaveOnlineDisplay.Dispose();
        M<Saver>().SaveAction = default;        
    }
    

    protected override void OnModuleActivate()
    {
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