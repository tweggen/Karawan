using System;
using System.Collections.Generic;
using System.Numerics;
using engine.geom;
using static engine.Logger;

namespace nogame;

public class GameState
{
    //static public DateTime GameT0 = new DateTime(1982, 3, 12, 22, 46, 0);
    static public DateTime GameT0 = new DateTime(1982, 3, 12, 19, 0, 0);
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

    /// <summary>
    /// Serialized TALE NPC deviations (JSON array of deviated NpcSchedule objects).
    /// Only NPCs the player has interacted with and changed are stored here.
    /// </summary>
    public string TaleDeviations { get; set; } = "";

    /// <summary>
    /// TALE-SOCIAL Phase E6: serialized per-cluster social state (group
    /// tables + per-NPC social snapshots). Additive and optional — old saves
    /// deserialize with the empty default and simply re-derive social state
    /// from the baked scenarios.
    /// </summary>
    public string TaleSocialState { get; set; } = "";

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
            /*
             * Read BEFORE assigning. This message used to print PlayerPosition after it had
             * already been overwritten, so it always said "from X to X" and hid what was wrong.
             */
            Error($"Adjusting GameState PlayerPosition from {PlayerPosition} to {PlayerPos0}.");

            PlayerPosition = new(PlayerPos0);
            PlayerOrientation = new(PlayerOrientation0);
        }

        /*
         * The orientation gets its OWN check, because until now it only ever got repaired as a
         * side effect of the position being out of bounds - so a save with a perfectly good
         * position and a broken orientation survived every load untouched. That is the exact
         * combination observed on device: GetPlayerPosition reported a NaN orientation at
         * startup while the position check stayed quiet.
         *
         * Note default(Quaternion) is (0,0,0,0), NOT identity, and Vector3.Transform happily
         * accepts it - it does not require a unit quaternion, it scales by |q|^2. So an
         * orientation that was never initialised does not announce itself; it silently turns
         * every derived front vector into the zero vector, and the first normalisation of that
         * produces NaN. Validating length here is what makes an uninitialised orientation a
         * logged, repaired condition rather than a black screen several subsystems later.
         */
        float lOrientation = ((Quaternion)PlayerOrientation).Length();
        if (!(Single.Abs(lOrientation - 1f) <= 1e-3f))
        {
            Error($"Adjusting GameState PlayerOrientation from {PlayerOrientation} "
                  + $"(length {lOrientation}) to {PlayerOrientation0}.");

            PlayerOrientation = new(PlayerOrientation0);
        }

        if (GameNow.Year < GameT0.Year)
        {
            Error($"Adjusting GameState GameNow from {GameNow} to {GameT0}.");
            GameNow = GameT0;
        }
    }
}