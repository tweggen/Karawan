using System.Collections.Generic;
using System.Numerics;

namespace nogame;

public class GameState
{
    public int Id { get; set; }
    public Vector3 PlayerPosition { get; set; }
    public Quaternion PlayerOrientation { get; set; }
    public int NumberCubes { get; set; }
    public int NumberPolytopes { get; set; }
    public int Health { get; set; }
}