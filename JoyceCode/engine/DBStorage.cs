using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
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
            vector => new BsonArray(new BsonValue[] { vector.X, vector.Y }),
            value => new Vector2(
                (float)value.AsArray[0].AsDouble,
                (float)value.AsArray[1].AsDouble)
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
        Expression<Func<ObjType, bool>>? predicate,
        out IEnumerable<ObjType> c) where ObjType : class
    {
        bool haveIt = false;
        c = null;
        try
        {
            var col = db.GetCollection<ObjType>();
            if (0 == col.Count())
            {
                Trace($"No collection found for {typeof(ObjType)}");
                return false;
            }

            try
            {
                IEnumerable<ObjType> allObjects;
                if (null == predicate)
                {
                    allObjects = col.FindAll();
                }
                else
                {
                    allObjects = col.Find(predicate);
                }
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
        Action<ILiteCollection<ObjType>>? actionPrepare,
        IEnumerable<ObjType> c) where ObjType : class
    {
        if (c == null)
        {
            ErrorThrow($"The collection we store {c.GetType()} os null.", m => new ArgumentException(m));
            return;
        }
        var col = db.GetCollection<ObjType>();
        if (actionPrepare != null)
        {
            actionPrepare(col);
        }
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
    

    private bool _withOpen(string dbName, Action<LiteDatabase> action)
    {
        lock (_lo)
        {
            try
            {
                LiteDatabase db = _open(dbName);
                try
                {
                    action(db);
                }
                catch (Exception e)
                {
                    Error($"Unable to execute action: {e}");
                }

                _close(dbName);
                return true;
            }
            catch (Exception e)
            {
                Error($"Unable to open/close database: {e}");
            }
        }

        return false;
    }

    
    public void SaveGameState<GS>(GS gameState) where GS : class
    {
        _withOpen(DbGameState, db => _writeObject(db, gameState));
    }
    

    public bool LoadGameState<GS>(out GS gameState) where GS : class
    {
        bool haveIt = false;
        GS resultData = null;
        _withOpen(DbGameState, db =>
        {
            haveIt = _readObject(db, out resultData);
        });
        gameState = resultData;
        return haveIt;
    }
    

    /**
     * Update parts of the collection using the input objects given.
     */
    public bool UpdateCollection<ObjType>(IEnumerable<ObjType> obj,
        Action<ILiteCollection<ObjType>> actionPrepare) where ObjType : class
    {
        return _withOpen(DbWorldCache, db =>
        {
            _writeCollection(db, actionPrepare, obj);
        });
    }
    
    
    public bool StoreCollection<ObjType>(IEnumerable<ObjType> obj) where ObjType : class
    {
        return _withOpen(DbWorldCache, db =>
        {
            _writeCollection(db, col => col.DeleteAll(), obj);
        });
    }
    

    public bool LoadCollection<ObjType>(Expression<Func<ObjType,bool>>? predicate, out IEnumerable<ObjType> o) where ObjType : class
    {
        bool haveIt = false;
        IEnumerable<ObjType> resultData = null;
        _withOpen(DbWorldCache, db =>
        {
            haveIt = _readCollection(db, predicate, out resultData);
        });
        o = resultData;
        return haveIt;
    }


    public bool LoadCollection<ObjType>(out IEnumerable<ObjType> o) where ObjType : class
    {
        return LoadCollection(null, out o);
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