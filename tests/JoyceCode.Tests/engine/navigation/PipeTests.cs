using System;
using System.Collections.Generic;
using System.Numerics;
using Xunit;
using JoyceCode.engine.navigation;
using builtin.modules.satnav.desc;

namespace JoyceCode.Tests.engine.navigation;

public class PipeTests
{
    private NavJunction CreateJunction(Vector3 position)
    {
        return new NavJunction
        {
            Position = position,
            StartingLanes = new(),
            EndingLanes = new()
        };
    }

    [Fact]
    public void Pipe_ComputeLength_CalculatesCorrectly()
    {
        var j1 = CreateJunction(Vector3.Zero);
        var j2 = CreateJunction(new Vector3(10, 0, 0));
        var j3 = CreateJunction(new Vector3(20, 0, 0));

        var lane1 = new NavLane { Start = j1, End = j2, Length = 10.0f };
        var lane2 = new NavLane { Start = j2, End = j3, Length = 10.0f };

        var pipe = new Pipe
        {
            NavLanes = new List<NavLane> { lane1, lane2 }
        };
        pipe.ComputeLength();

        Assert.Equal(20, pipe.Length, precision: 0.1f);
    }

    [Fact]
    public void Pipe_GetSpeedAt_WithoutConstraint_ReturnsSpeedFunctionValue()
    {
        var pipe = new Pipe
        {
            SupportedType = TransportationType.Car,
            SpeedFunction = (pos, time) => 5.0f
        };

        var speed = pipe.GetSpeedAt(Vector3.Zero, DateTime.Now);

        Assert.Equal(5.0f, speed);
    }

    [Fact]
    public void Pipe_GetSpeedAt_WithBlockingConstraint_ReturnsZero()
    {
        var constraint = new CyclicConstraint
        {
            CycleSeconds = 60,
            ActivePhaseStart = 0,
            ActivePhaseDuration = 30
        };

        var pipe = new Pipe
        {
            SupportedType = TransportationType.Car,
            GlobalConstraint = constraint,
            SpeedFunction = (pos, time) => 5.0f
        };

        // Query at time when constraint is blocked (t=45s)
        var time = DateTime.UnixEpoch.AddSeconds(45);
        var speed = pipe.GetSpeedAt(Vector3.Zero, time);

        Assert.Equal(0.0f, speed);
    }

    [Fact]
    public void Pipe_GetSpeedAt_NoConstraintReturnsDefaultSpeed()
    {
        var pipe = new Pipe
        {
            SupportedType = TransportationType.Pedestrian
        };

        var speed = pipe.GetSpeedAt(Vector3.Zero, DateTime.Now);

        Assert.Equal(1.5f, speed);  // Default pedestrian speed
    }

    [Fact]
    public void PipeNetwork_FindPipeContaining_ReturnsCorrectPipe()
    {
        var pipe1 = new Pipe
        {
            Id = 1,
            StartPosition = Vector3.Zero,
            EndPosition = new Vector3(100, 0, 0),
            Length = 100
        };

        var pipe2 = new Pipe
        {
            Id = 2,
            StartPosition = new Vector3(100, 0, 0),
            EndPosition = new Vector3(200, 0, 0),
            Length = 100
        };

        var network = new PipeNetwork
        {
            Pipes = new List<Pipe> { pipe1, pipe2 }
        };

        var found = network.FindPipeContaining(new Vector3(50, 0, 0));

        Assert.NotNull(found);
        Assert.Equal(1, found.Id);
    }

    [Fact]
    public void PipeNetwork_FindOutgoingPipes_ReturnsConnected()
    {
        var pipe1 = new Pipe { Id = 1, EndPosition = new Vector3(100, 0, 0) };
        var pipe2 = new Pipe { Id = 2, StartPosition = new Vector3(100, 0, 0) };

        var network = new PipeNetwork
        {
            Pipes = new List<Pipe> { pipe1, pipe2 }
        };

        var outgoing = network.FindOutgoingPipes(pipe1);

        Assert.Contains(pipe2, outgoing);
    }

    [Fact]
    public void PipeNetwork_GetTotalEntityCount_ReturnsCorrectCount()
    {
        var pipe1 = new Pipe { Id = 1 };
        var pipe2 = new Pipe { Id = 2 };

        pipe1.Entities.Enqueue(new MovingEntity { Id = 1 });
        pipe1.Entities.Enqueue(new MovingEntity { Id = 2 });
        pipe2.Entities.Enqueue(new MovingEntity { Id = 3 });

        var network = new PipeNetwork
        {
            Pipes = new List<Pipe> { pipe1, pipe2 }
        };

        Assert.Equal(3, network.GetTotalEntityCount());
    }
}
