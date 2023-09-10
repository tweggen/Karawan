using engine;
using LiteDB;

namespace nogame;

public class DBStorage
{
    private LiteDatabase _db;


    public void SaveGameState(GameState gameState)
    {
        _db.
    }


    public void Close()
    {
        _db.Commit();
        _db.Dispose();
    }
    
    
    public void Open()
    {
        string path = GlobalSettings.Get("Engine.RWPath");
        string dbname = "gamestate.db";

        _db = new LiteDatabase(path+dbname);
    }
    
    
    
    public DBStorage()
    {
    }
}