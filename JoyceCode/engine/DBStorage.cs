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

namespace engine;

public class DBStorage : IDisposable
{
    private object _lo = new();

    private Dictionary<string, LiteDatabase> _mapDBs = new();

    private BsonMapper _mappers;


    private const string DbFileSuffix = ".db";
    private const string DbGameState = "gamestate"; 
    private const string DbWorldCache = "worldcache"; 
    
    
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
    
    
    
    
    
    private bool _readObject<ObjType>(LiteDatabase db, out ObjType gameState) where ObjType : class
    {
        bool haveIt = false;
        gameState = null;
        try
        {
            var col = db.GetCollection<ObjType>();
            Trace($"Collection has {col.Count()}: {col}");
            var allGameStates = col.FindAll();
            ObjType? foundGameState = col.FindById(1);
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
    

    private void _writeObject<ObjType>(LiteDatabase db, ObjType gameState) where ObjType : class
    {
        if (gameState == null)
        {
            ErrorThrow("GameState is null", m => new ArgumentNullException(m));
        }
        var col = db.GetCollection<ObjType>();
        col.Upsert(gameState);
        db.Commit();
    }
    

    private bool _readCollection<ObjType>(
        LiteDatabase db,
        out IEnumerable<ObjType> c) where ObjType : class
    {
        bool haveIt = false;
        c = null;
        try
        {
            var col = db.GetCollection<ObjType>();
            
            Trace($"Collection has {col.Count()}: {col}");
            if (0 == col.Count())
            {
                Trace($"No collection found for {typeof(ObjType)}");
                return false;
            }

            try
            {
                var allObjects = col.FindAll();
                c = new List<ObjType>(allObjects);
                haveIt = true;
            }
            catch (Exception e)
            {
                /*
                 * If we have an exception here we better delete this collection.
                 */
                db.DropCollection(typeof(ObjType).Name);
                db.Commit();
            }
        }
        catch (Exception e)
        {
            Error($"Unable to load collection {c.GetType()}: {e}");
        }

        return haveIt;
    }
    

    private void _writeCollection<ObjType>(
        LiteDatabase db,
        IEnumerable<ObjType> c) where ObjType : class
    {
        if (c == null)
        {
            ErrorThrow($"The collection we store {c.GetType()} os null.", m => new ArgumentException(m));
            return;
        }

        var col = db.GetCollection<ObjType>();
        col.DeleteAll();
        col.Insert(c);
        db.Commit();
    }
    

    private void _close(string dbName)
    {
        LiteDatabase db = _mapDBs[dbName];
        if (null != db)
        {
            db.Commit();
            db.Dispose();
            db = null;
            _mapDBs[dbName] = null;
        }
    }
    
    
    private LiteDatabase _open(string dbName)
    {
        LiteDatabase db;
        if (_mapDBs.TryGetValue(dbName, out db)) 
        {
            if (db != null)
            {
                ErrorThrow($"I did not expect db to be open here.", m => new InvalidOperationException(m));
                return null;
            }
        }
        string path = GlobalSettings.Get("Engine.RWPath");
        string dbFileName = dbName + DbFileSuffix;

        db = new LiteDatabase(Path.Combine(path, dbFileName), _mappers);
        _mapDBs[dbName] = db;
        return db;
    }



    public void SaveGameState<GS>(GS gameState) where GS : class
    {
        lock (_lo)
        {
            try
            {
                LiteDatabase db = _open(DbGameState);
                try
                {
                    _writeObject(db, gameState);
                }
                catch (Exception e)
                {
                    Error($"Unable to write gameState: {e}");
                }

                _close("gamestate");
            }
            catch (Exception e)
            {
                Error($"Unable to open/close database: {e}");
            }
        }
    }
    

    public bool LoadGameState<GS>(out GS gameState) where GS : class
    {
        bool haveIt = false;
        lock (_lo)
        {
            gameState = null;
            try
            {
                LiteDatabase db = _open(DbGameState);
                try
                {
                    haveIt = _readObject(db, out gameState);
                }
                catch (Exception e)
                {
                    Error($"Unable to write gameState: {e}");
                }

                _close(DbGameState);
            }
            catch (Exception e)
            {
                Error($"Unable to open/close database: {e}");
            }
        }

        return haveIt;
    }
    
    
    public bool StoreCollection<ObjType>(IEnumerable<ObjType> obj) where ObjType : class
    {
        lock (_lo)
        {
            try
            {
                LiteDatabase db = _open(DbWorldCache);
                try
                {
                    _writeCollection<ObjType>(db, obj);
                }
                catch (Exception e)
                {
                    Error($"Unable to write collection of {obj.GetType()}: {e}");
                }

                _close(DbWorldCache);
                return true;
            }
            catch (Exception e)
            {
                Error($"Unable to open/close database: {e}");
            }
        }

        return false;
    }
    

    public bool LoadCollection<ObjType>(out IEnumerable<ObjType> o) where ObjType : class
    {
        bool haveIt = false;
        lock (_lo)
        {
            o = null;
            try
            {
                LiteDatabase db = _open(DbWorldCache);
                try
                {
                    haveIt = _readCollection(db, out o);
                }
                catch (Exception e)
                {
                    Error($"Unable to write {o.GetType()}: {e}");
                }

                _close(DbWorldCache);
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
        foreach (var dbName in _mapDBs.Keys)
        {
            LiteDatabase db = _mapDBs[dbName];
            if (null != db)
            {
                _close(dbName);
            }
        }
    }


    public DBStorage()
    {
        _mappers = _createMappers();
    }
}