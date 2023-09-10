using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
using engine;
using LiteDB;
using static engine.Logger;

namespace nogame;

public class DBStorage : IDisposable
{
    private object _lo = new();
    private LiteDatabase _db;


    private bool _readGameState(out GameState gameState)
    {
        bool haveIt = false;
        gameState = null;
        try
        {
            var col = _db.GetCollection<GameState>();
            Trace($"Collection has {col.Count()}: {col}");
            var allGameStates = col.FindAll();
            GameState? foundGameState = col.FindOne((x) => x.Id == "0");
            if (foundGameState != null)
            {
                gameState = foundGameState;
                haveIt = true;
            }
        }
        catch (Exception e)
        {
            Error($"Unable to load previous savegagme: {e}");
        }

        return haveIt;
    }
    

    private void _writeGameState(GameState gameState)
    {
        if (gameState == null)
        {
            ErrorThrow("GameState is null", m => new ArgumentNullException(m));
        }
        var col = _db.GetCollection<GameState>();
        col.Upsert(gameState);
        _db.Commit();
    }


    public void Close()
    {
        if (null != _db)
        {
            _db.Commit();
            _db.Dispose();
            _db = null;
        }
    }
    
    
    public void Open()
    {
        string path = GlobalSettings.Get("Engine.RWPath");
        string dbname = "gamestate.db";

        _db = new LiteDatabase(path+dbname);
    }



    public void SaveGameState(GameState gameState)
    {
        lock (_lo)
        {
            try
            {
                Open();
                try
                {
                    _writeGameState(gameState);
                }
                catch (Exception e)
                {
                    Error($"Unable to write gameState: {e}");
                }

                Close();
            }
            catch (Exception e)
            {
                Error($"Unable to open/close database: {e}");
            }
        }
    }
    

    public bool LoadGameState(out GameState gameState)
    {
        bool haveIt = false;
        lock (_lo)
        {
            gameState = null;
            try
            {
                Open();
                try
                {
                    haveIt = _readGameState(out gameState);
                }
                catch (Exception e)
                {
                    Error($"Unable to write gameState: {e}");
                }

                Close();
            }
            catch (Exception e)
            {
                Error($"Unable to open/close database: {e}");
            }
        }

        return haveIt;
    }
    

    public void Dispose()
    {
        Close();
    }
}