using System;
using System.Collections.Generic;
using System.Numerics;

namespace nogame;

public class GameState
{
    [LiteDB.BsonId]
    public int Id { get; set; } = 1;
    public Vector3 PlayerPosition { get; set; } = Vector3.Zero;
    public Quaternion PlayerOrientation { get; set; } = Quaternion.Identity;
    public int NumberCubes { get; set; } = 0;
    public int NumberPolytopes { get; set; } = 0;
    public int Health { get; set; } = 1000;

    public DateTime GameNow { get; set; } = new DateTime(1982, 3, 12, 22, 46, 0);
    
    public bool IsValid()
    {
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
            PlayerPosition = new Vector3(0f, 100f, 0f);
        }
    }
}