using System;
using System.Numerics;

namespace engine.quest;


public class TrailVehicle : ToSomewhere
{
    public TrailVehicle()
    {
        RelativePosition = new Vector3(0f, 3f, 0f);
    }
}