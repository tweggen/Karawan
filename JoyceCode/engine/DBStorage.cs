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
    
    private bool _readGameState<GS>(out GS gameState) where GS : class
    {
        bool haveIt = false;
        gameState = null;
        try
        {
            var col = _db.GetCollection<GS>();
            Trace($"Collection has {col.Count()}: {col}");
            var allGameStates = col.FindAll();
            GS? foundGameState = col.FindById(1);
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
    

    private void _writeGameState<GS>(GS gameState) where GS : class
    {
        if (gameState == null)
        {
            ErrorThrow("GameState is null", m => new ArgumentNullException(m));
        }
        var col = _db.GetCollection<GS>();
        col.Upsert(gameState);
        _db.Commit();
    }


    private bool _readCollection<ObjType>(out IEnumerable<ObjType> c) where ObjType : class
    {
        bool haveIt = false;
        c = null;
        try
        {
            var col = _db.GetCollection<ObjType>();
            
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
                _db.DropCollection(typeof(ObjType).Name);
                _db.Commit();
            }
        }
        catch (Exception e)
        {
            Error($"Unable to load collection {c.GetType()}: {e}");
        }

        return haveIt;
    }
    

    private void _writeCollection<ObjType>(IEnumerable<ObjType> c) where ObjType : class
    {
        if (c == null)
        {
            ErrorThrow($"The collection we store {c.GetType()} os null.", m => new ArgumentException(m));
            return;
        }

        var col = _db.GetCollection<ObjType>();
        col.DeleteAll();
        col.Insert(c);
        _db.Commit();
    }
    

    private void _close()
    {
        if (null != _db)
        {
            _db.Commit();
            _db.Dispose();
            _db = null;
        }
    }
    
    
    private void _open()
    {
        if (null != _db)
        {
            ErrorThrow($"I did not expect db to be open here.", m => new InvalidOperationException(m));
        }
        string path = GlobalSettings.Get("Engine.RWPath");
        string dbname = "gamestate.db";

        _db = new LiteDatabase(Path.Combine(path, dbname), _mappers);
    }



    public void SaveGameState<GS>(GS gameState) where GS : class
    {
        lock (_lo)
        {
            try
            {
                _open();
                try
                {
                    _writeGameState(gameState);
                }
                catch (Exception e)
                {
                    Error($"Unable to write gameState: {e}");
                }

                _close();
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
                _open();
                try
                {
                    haveIt = _readGameState(out gameState);
                }
                catch (Exception e)
                {
                    Error($"Unable to write gameState: {e}");
                }

                _close();
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
                _open();
                try
                {
                    _writeCollection<ObjType>(obj);
                }
                catch (Exception e)
                {
                    Error($"Unable to write collection of {obj.GetType()}: {e}");
                }

                _close();
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
                _open();
                try
                {
                    haveIt = _readCollection(out o);
                }
                catch (Exception e)
                {
                    Error($"Unable to write {o.GetType()}: {e}");
                }

                _close();
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
        _close();
    }


    public DBStorage()
    {
        _mappers = _createMappers();
    }
}