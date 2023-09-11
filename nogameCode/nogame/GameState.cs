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
}