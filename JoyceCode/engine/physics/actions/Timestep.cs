using System;
using System.Diagnostics;
using BepuPhysics;

namespace engine.physics.actions;

public class Timestep : ABase
{
    public float DeltaTime;
    
    static public int Execute(Log? plog, Simulation simulation, float deltaTime)
    {
        bool hadException = false;
        try
        {
            simulation.Timestep(deltaTime);
        }
        catch (Exception e)
        {
            hadException = true;
        }

        if (plog != null)
        {
            plog.Append(new Timestep()
            {
                DeltaTime = deltaTime
            });
        }

        if (hadException)
        {
            if (plog != null)
            {
                plog.Dump();
            }
        }

        return 0;
    }
    
    
    public override int Execute(Player player, Simulation simulation)
    {
        return Execute(null, simulation, DeltaTime);
    }
}