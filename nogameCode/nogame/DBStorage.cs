using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.JavaScript;
using engine;
using LiteDB;
using static engine.Logger;

namespace nogame;

public class DBStorage : IDisposable
{
    private object _lo = new();
    private LiteDatabase _db;
    private BsonMapper _mappers;

    private BsonMapper _createMappers()
    {
        BsonMapper m = new();
        m.RegisterType(
            vector => new BsonArray(new BsonValue[] { vector.X, vector.Y, vector.Z }),
            value => new Vector3(
                (float)value.AsArray[0].AsDouble,
                (float)value.AsArray[1].AsDouble,
                (float)value.AsArray[2].AsDouble)
        );
        m.RegisterType(
            quat => new BsonArray(new BsonValue[] { quat.X, quat.Y, quat.Z, quat.W }),
            value => new Quaternion(
                    (float)value.AsArray[0].AsDouble,
                    (float)value.AsArray[1].AsDouble,
                    (float)value.AsArray[2].AsDouble,
                    (float)value.AsArray[3].AsDouble)
        );
        return m;
    }
    
    private bool _readGameState(out GameState gameState)
    {
        bool haveIt = false;
        gameState = null;
        try
        {
            var col = _db.GetCollection<GameState>();
            Trace($"Collection has {col.Count()}: {col}");
            var allGameStates = col.FindAll();
            GameState? foundGameState = col.FindById(1);
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

        _db = new LiteDatabase(Path.Combine(path, dbname), _mappers);
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


    public DBStorage()
    {
        _mappers = _createMappers();
    }
}