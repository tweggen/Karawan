using System;
using System.Collections.Generic;
using System.Numerics;
using Xunit;
using engine.navigation;
using builtin.modules.satnav.desc;

namespace JoyceCode.Tests.engine.navigation;

public class PipeSubdivisionTests
{
    [Fact]
    public void AddObstruction_CreatesSubdivision()
    {
        var pipe = new Pipe { Id = 1 };

        pipe.AddObstruction(
            new Vector3(50, 0, 0),
            5.0f,
            (pos, time) => 1.0f,
            "Accident");

        Assert.NotNull(pipe.Subdivisions);
        Assert.Single(pipe.Subdivisions);
        Assert.Equal("Accident", pipe.Subdivisions[0].Reason);
    }

    [Fact]
    public void Subdivision_ContainsPosition_WorksCorrectly()
    {
        var sub = new PipeSubdivision
        {
            StartPosition = new Vector3(45, 0, 0),
            EndPosition = new Vector3(55, 0, 0)
        };

        Assert.True(sub.ContainsPosition(new Vector3(50, 0, 0)));
        Assert.False(sub.ContainsPosition(new Vector3(30, 0, 0)));
    }

    [Fact]
    public void RemoveObstruction_ClearsSubdivision()
    {
        var pipe = new Pipe { Id = 1 };
        var obstaclePos = new Vector3(50, 0, 0);

        pipe.AddObstruction(obstaclePos, 5.0f, (pos, time) => 1.0f);
        Assert.Single(pipe.Subdivisions);

        pipe.RemoveObstruction(obstaclePos);
        Assert.Empty(pipe.Subdivisions);
    }

    [Fact]
    public void Pipe_GetSpeedAt_InSubdivision_ReturnsSubdivisionSpeed()
    {
        var pipe = new Pipe
        {
            SpeedFunction = (pos, time) => 10.0f
        };

        pipe.AddObstruction(
            new Vector3(50, 0, 0),
            5.0f,
            (pos, time) => 2.0f);  // Slow speed in obstruction

        var normalSpeed = pipe.GetSpeedAt(new Vector3(10, 0, 0), DateTime.Now);
        var slowSpeed = pipe.GetSpeedAt(new Vector3(50, 0, 0), DateTime.Now);

        Assert.Equal(10.0f, normalSpeed);
        Assert.Equal(2.0f, slowSpeed);
    }

    [Fact]
    public void HasSubdivisions_ReflectsState()
    {
        var pipe = new Pipe { Id = 1 };
        Assert.False(pipe.HasSubdivisions);

        pipe.AddObstruction(new Vector3(50, 0, 0), 5.0f, (pos, time) => 2.0f);
        Assert.True(pipe.HasSubdivisions);

        pipe.ClearSubdivisions();
        Assert.False(pipe.HasSubdivisions);
    }

    [Fact]
    public void ClearSubdivisions_RemovesAllSubdivisions()
    {
        var pipe = new Pipe { Id = 1 };

        pipe.AddObstruction(new Vector3(50, 0, 0), 5.0f, (pos, time) => 2.0f);
        pipe.AddObstruction(new Vector3(100, 0, 0), 3.0f, (pos, time) => 3.0f);

        Assert.Equal(2, pipe.Subdivisions.Count);

        pipe.ClearSubdivisions();
        Assert.Empty(pipe.Subdivisions);
    }
}
