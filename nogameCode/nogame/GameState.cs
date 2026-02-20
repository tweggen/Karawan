using System;
using System.Numerics;
using engine.geom;
using static engine.Logger;

namespace nogame;

public class GameState
{
    static public DateTime GameT0 = new DateTime(1982, 3, 12, 22, 46, 0);
    static public Vector3 PlayerPos0 = new Vector3(0f, 150f, 0f);
    static public Quaternion PlayerOrientation0 = Quaternion.CreateFromAxisAngle(Vector3.UnitY, Single.Pi);

    [LiteDB.BsonId]
    public int Id { get; set; } = 2;
    public SerializableVector3 PlayerPosition { get; set; } = SerializableVector3.Zero;
    public SerializableQuaternion PlayerOrientation { get; set; } = SerializableQuaternion.Identity;
    public int NumberCubes { get; set; } = 0;
    public int NumberPolytopes { get; set; } = 0;
    public int Health { get; set; } = 1000;
    
    /**
     * 0 currently means hover, 1 means walk.
     */
    public int PlayerEntity { get; set; } = 0;

    public string Story { get; set; } = "";

    public string FollowedQuestId { get; set; } = null;
    
    public string Entities { get; set; } = "";
    
    private DateTime _gameNow = GameT0;
    public DateTime GameNow
    {
        get
        {
            return _gameNow;
        }
        set
        {
            _gameNow = value;
        }
    }
    
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
            PlayerPosition = new(PlayerPos0);
            PlayerOrientation = new(PlayerOrientation0);
            
            Error($"Adjusting GameState PlayerPosition from {PlayerPosition} to {PlayerPos0}.");
        }

        if (GameNow.Year < GameT0.Year)
        {
            Error($"Adjusting GameState GameNow from {GameNow} to {GameT0}.");
            GameNow = GameT0;
        }
    }
}