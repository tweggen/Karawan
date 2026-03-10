using System;
using System.Collections.Generic;
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
    public int Cash { get; set; } = 0;

    /**
     * The player mode, e.g. "hover", "walking".
     * Legacy: PlayerEntity 0 means hover, 1 means walk.
     */
    public string PlayerMode { get; set; } = "hover";

    /**
     * 0 currently means hover, 1 means walk.
     */
    public int PlayerEntity { get; set; } = 0;

    /**
     * The currently active narration script state (JSON).
     */
    public string Story { get; set; } = "";

    /**
     * The quest currently being followed for satnav/markers.
     */
    public string FollowedQuestId { get; set; } = null;

    /**
     * List of quest IDs the player is following.
     * Redundant with entity persistence but kept for quick access.
     */
    public List<string> FollowedQuestIds { get; set; } = new();

    /**
     * List of strings describing world modifications applied by the player
     * or game events (e.g. "destroyed:building:cluster3:42", "unlocked:gate:north").
     */
    public List<string> WorldModifications { get; set; } = new();

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