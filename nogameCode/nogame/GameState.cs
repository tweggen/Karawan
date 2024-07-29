using System;
using System.Numerics;
using static engine.Logger;

namespace nogame;

public class GameState
{
    static public DateTime GameT0 = new DateTime(1982, 3, 12, 22, 46, 0);
    static public Vector3 PlayerPos0 = new Vector3(0f, 300f, 0f);

    [LiteDB.BsonId]
    public int Id { get; set; } = 1;
    public Vector3 PlayerPosition { get; set; } = Vector3.Zero;
    public Quaternion PlayerOrientation { get; set; } = Quaternion.Identity;
    public int NumberCubes { get; set; } = 0;
    public int NumberPolytopes { get; set; } = 0;
    public int Health { get; set; } = 1000;

    public DateTime GameNow { get; set; } = GameT0;
    
    public bool IsValid()
    {
        if (GameNow.Year < 1982)
        {
            return false;
        }
        
        if (!engine.world.MetaGen.AABB.Contains(PlayerPosition))
        {
            return false;
        }

        return true;
    }


    public void Fix()
    {
        if (!engine.world.MetaGen.AABB.Contains(PlayerPosition))
        {
            PlayerPosition = PlayerPos0;
            Error($"Adjusting GameState PlayerPosition from {PlayerPosition} to {PlayerPos0}.");
        }

        if (GameNow.Year < GameT0.Year)
        {
            Error($"Adjusting GameState GameNow from {GameNow} to {GameT0}.");
            GameNow = GameT0;
        }
    }
}