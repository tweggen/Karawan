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
    private static readonly engine.Dc _dc = engine.Dc.Database;

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
            ObjType? foundGameState = col.FindAll().FirstOrDefault();
            if (foundGameState != null)
            {
                gameState = foundGameState;
                haveIt = true;
            }
        }
        catch (Exception e)
        {
            Error($"Unable to load previous savegame: {e}");
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
    

    /**
     * Read a stored collection back, or say why not.
     *
     * ⚠️ BOTH catches here used to be silent, in two different ways, and both are the
     * suppressed-Warning lesson again: a failure nobody is told about is a failure that
     * costs somebody a round of investigation.
     *
     * The INNER catch DELETES the collection and returns false. That is the right repair
     * for data that cannot be read - see the block comment on the catch - but it was done
     * without a word of any level, not even a Trace. WP-B6 found what that hides:
     * ClusterDesc serialises its StreetHeightSource, FlatStreetHeight has no parameterless
     * constructor, and so the seventy cities of the shipped world have been written to the
     * cache and thrown away again on every single start since the cache was written, in
     * total silence. It only came to light because somebody went looking for something
     * else.
     *
     * The OUTER catch was worse than silent: it read `c.GetType()`, and `c` is the out
     * parameter, set to null on entry and assigned only on the success path INSIDE the
     * inner try - so on every path that can reach the outer catch `c` is null, and the
     * handler raised a NullReferenceException of its own, discarding the real exception
     * and propagating out of _readCollection instead of reporting anything. Measured, not
     * reasoned: DBStorage.cs:152, NullReferenceException, on a database whose Count()
     * throws. It has therefore never printed either. The type is named from typeof, which
     * cannot be null and is the ELEMENT type - what `c.GetType()` would have printed even
     * on a non-null value is List`1, which names nothing anybody wants to know.
     */
    internal bool _readCollection<ObjType>(
        LiteDatabase db,
        Expression<Func<ObjType, bool>>? predicate,
        out IEnumerable<ObjType> c) where ObjType : class
    {
        bool haveIt = false;
        c = null;
        try
        {
            var col = db.GetCollection<ObjType>();
            var nStored = col.Count();
            if (0 == nStored)
            {
                /*
                 * An absent cache is the ordinary case on a first start and is not a
                 * failure, so this one stays a Trace.
                 */
                Trace(_dc, $"No collection found for {typeof(ObjType)}");
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
                 * If we have an exception here we better delete this collection: it cannot
                 * be read, every caller regenerates what it could not load, and a stored
                 * collection that fails identically on every start for the life of the
                 * install is a permanent fault rather than a cache.
                 *
                 * ⚠️ REPORTED BEFORE IT IS DROPPED, deliberately. If DropCollection or
                 * Commit throws, the outer catch reports THAT, and the cause of the drop is
                 * already in the log rather than lost behind it.
                 *
                 * ⚠️ AND THIS IS NOT ALWAYS THE DATA'S FAULT, which is why the line names
                 * the predicate. This try covers materialising the documents AND
                 * translating the caller's predicate, and LiteDB cannot translate every
                 * expression that compiles - so a query bug on our side deletes intact
                 * stored data. Measured: a two-document collection, an untranslatable
                 * predicate, and the collection is gone. Nothing in the tree passes a
                 * predicate to LoadCollection today, so it is latent; changing when data is
                 * destroyed has every player's worldcache and gameconfig behind it and is
                 * not this change's to make.
                 */
                Error(_dc,
                    $"Unable to read collection {typeof(ObjType)}"
                    + (null == predicate ? "" : " with a predicate (which may itself be what failed)")
                    + $" - DISCARDING the {nStored} stored document(s), they will have to be regenerated: {e}");
                db.DropCollection(typeof(ObjType).Name);
                db.Commit();
            }
        }
        catch (Exception e)
        {
            Error(_dc, $"Unable to load collection {typeof(ObjType)}: {e}");
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
            /*
             * ⚠️ THE SAME DEFECT AS THE OUTER CATCH ABOVE, and here it was unconditional:
             * this branch runs exactly when c IS null, and it then read c.GetType(). So it
             * raised a NullReferenceException before ErrorThrow was ever called - no log
             * line, and the wrong exception type reaching the caller. typeof(ObjType) is
             * the element type and cannot be null.
             */
            ErrorThrow($"The collection we store of {typeof(ObjType)} is null.", m => new ArgumentException(m));
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
            Trace(_dc, $"Unable to use collection {typeof(ObjType).Name}, exception {e}, dropping and re-creating");
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