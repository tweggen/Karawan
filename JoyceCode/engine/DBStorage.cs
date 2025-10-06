using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using engine;
using LiteDB;
using static engine.Logger;

namespace engine;

public class DBStorage : engine.AModule
{

    private Dictionary<string, LiteDatabase> _mapDBs = new();

    public BsonMapper Mapper;


    private const string DbFileSuffix = ".db";
    private const string DbGameState = "gamestate";
    private const int DbVersion = 3;
    
    
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
#if false        
        m.RegisterType<DateTime>(
            value => value.ToString("o", CultureInfo.InvariantCulture),
            bson => DateTime.ParseExact(bson, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
        m.RegisterType<DateTimeOffset>(
            value => value.ToString("o", CultureInfo.InvariantCulture),
            bson => DateTimeOffset.ParseExact(bson, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
#else
        m.RegisterType<DateTime>(
            value =>
            {
                var doc = new BsonDocument();
                doc["ticks"] = value.Ticks;
                return doc;
            },
            doc =>
            {
                return new DateTime(doc["ticks"].AsInt64);
            });
#endif
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
    
    
    private LiteDatabase _open(string dbName, int dbVersion)
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
        string fullpath = Path.Combine(path, dbFileName);
        bool hadDb = File.Exists(fullpath);
        db = new LiteDatabase(fullpath, Mapper);
        if (hadDb)
        {
            if (dbVersion != 0 && db.UserVersion < dbVersion)
            {
                Error($"Incompatible ({db.UserVersion}<{dbVersion}) database version of {dbName} detected, deleting content.");
                db.Dispose();
                File.Delete(fullpath);
                db = new LiteDatabase(fullpath, Mapper);
                db.UserVersion = dbVersion;
            }

        }
        else
        {
            db.UserVersion = dbVersion;
        }

        _mapDBs[dbName] = db;
        return db;
    }
    

    public bool WithOpen(string dbName, int dbVersion, Action<LiteDatabase> action)
    {
        lock (_lo)
        {
            try
            {
                LiteDatabase db = _open(dbName, dbVersion);
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


    public void WithCollection<ObjType>(ILiteDatabase db, Action<ILiteCollection<ObjType>> action) where ObjType : class
    {
        ILiteCollection<ObjType> col;
        try
        {
            col = db.GetCollection<ObjType>();
        }
        catch (Exception e)
        {
            Trace($"Unable to use collection {typeof(ObjType).Name}, exception {e}, dropping and re-creating");
            db.DropCollection(typeof(ObjType).Name);
            db.Commit();
            try
            {
                col = db.GetCollection<ObjType>();
            }
            catch (Exception f)
            {
                ErrorThrow($"Unable to re-create collection {typeof(ObjType).Name}, exception {f} giving up.", m => new InvalidOperationException(m));
                return;
            }
        }

        if (null == col)
        {
            ErrorThrow($"Unable to open collection {typeof(ObjType).Name}, giving up.", m=>new InvalidOperationException(m));
            return;
        }

        try
        {
            action(col);
        }
        catch (Exception e)
        {
            ErrorThrow( $"Exception {e} running action on collection {typeof(ObjType).Name} ", m => new InvalidOperationException(m));
            return;
        }
    }

    
    public void SaveGameState<GS>(GS gameState) where GS : class
    {
        WithOpen(DbGameState, DbVersion, db => _writeObject(db, gameState));
    }
    

    public bool LoadGameState<GS>(out GS gameState) where GS : class
    {
#if false
        gameState = null;
        return false;
#else
        bool haveIt = false;
        GS resultData = null;
        WithOpen(DbGameState, DbVersion, db =>
        {
            haveIt = _readObject(db, out resultData);
        });
        gameState = resultData;
        return haveIt;
#endif
    }
    

   public bool StoreCollection<ObjType>(string dbName, IEnumerable<ObjType> obj) where ObjType : class
    {
        return WithOpen(dbName, DbVersion, db =>
        {
            _writeCollection(db, col => col.DeleteAll(), obj);
        });
    }
    

    public bool LoadCollection<ObjType>(string dbName, Expression<Func<ObjType,bool>>? predicate, out IEnumerable<ObjType> o) where ObjType : class
    {
        bool haveIt = false;
        IEnumerable<ObjType> resultData = null;
        WithOpen(dbName, DbVersion, db =>
        {
            haveIt = _readCollection(db, predicate, out resultData);
        });
        o = resultData;
        return haveIt;
    }


    public bool LoadCollection<ObjType>(string dbName, out IEnumerable<ObjType> o) where ObjType : class
    {
        return LoadCollection(dbName,null, out o);
    }
    
    
    public override void Dispose()
    {
        foreach (var dbName in _mapDBs.Keys)
        {
            LiteDatabase db = _mapDBs[dbName];
            if (null != db)
            {
                _close(dbName);
            }
        }

        base.Dispose();
    }
    
    
    public DBStorage()
    {
        Mapper = _createMappers();
    }
}