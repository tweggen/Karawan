using System;
using System.Collections.Generic;
using System.Numerics;
using Xunit;
using JoyceCode.engine.navigation;
using builtin.modules.satnav.desc;

namespace JoyceCode.Tests.engine.navigation;

public class PipeControllerTests
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
    public void PlaceEntity_AddsEntityToPipe()
    {
        var j1 = CreateJunction(Vector3.Zero);
        var j2 = CreateJunction(new Vector3(10, 0, 0));

        var lane = new NavLane { Start = j1, End = j2, Length = 10.0f };
        var pipe = new Pipe
        {
            Id = 1,
            NavLanes = new List<NavLane> { lane },
            StartPosition = Vector3.Zero,
            EndPosition = new Vector3(10, 0, 0),
            Length = 10
        };

        var network = new PipeNetwork { Pipes = new List<Pipe> { pipe } };
        var controller = new PipeController(network);

        var entity = new MovingEntity { Id = 1, Route = new List<NavLane> { lane } };
        controller.PlaceEntity(entity);

        Assert.NotNull(entity.CurrentPipe);
        Assert.Equal(1, pipe.CurrentOccupancy);
    }

    [Fact]
    public void UpdateFrame_MovesEntityForward()
    {
        var j1 = CreateJunction(Vector3.Zero);
        var j2 = CreateJunction(new Vector3(100, 0, 0));

        var lane = new NavLane { Start = j1, End = j2, Length = 100.0f };
        var pipe = new Pipe
        {
            Id = 1,
            NavLanes = new List<NavLane> { lane },
            StartPosition = Vector3.Zero,
            EndPosition = new Vector3(100, 0, 0),
            Length = 100,
            SpeedFunction = (pos, time) => 10.0f  // 10 m/s
        };

        var network = new PipeNetwork { Pipes = new List<Pipe> { pipe } };
        var controller = new PipeController(network);

        var entity = new MovingEntity { Id = 1, Route = new List<NavLane> { lane } };
        controller.PlaceEntity(entity);

        var initialPos = entity.Position;
        controller.UpdateFrame(1.0f, DateTime.Now);  // 1 second at 10 m/s = 10m

        Assert.True(entity.Position.X > initialPos.X);
        Assert.Equal(10.0f, entity.Position.X - initialPos.X, precision: 0.1f);
    }

    [Fact]
    public void UpdateFrame_StopsEntityWhenBlocked()
    {
        var constraint = new CyclicConstraint
        {
            CycleSeconds = 60,
            ActivePhaseStart = 60,  // Always blocked
            ActivePhaseDuration = 0
        };

        var j1 = CreateJunction(Vector3.Zero);
        var j2 = CreateJunction(new Vector3(100, 0, 0));

        var lane = new NavLane { Start = j1, End = j2, Length = 100.0f };
        var pipe = new Pipe
        {
            Id = 1,
            NavLanes = new List<NavLane> { lane },
            StartPosition = Vector3.Zero,
            EndPosition = new Vector3(100, 0, 0),
            Length = 100,
            GlobalConstraint = constraint,
            SpeedFunction = (pos, time) => 10.0f
        };

        var network = new PipeNetwork { Pipes = new List<Pipe> { pipe } };
        var controller = new PipeController(network);

        var entity = new MovingEntity { Id = 1, Route = new List<NavLane> { lane } };
        controller.PlaceEntity(entity);

        var initialPos = entity.Position;
        controller.UpdateFrame(1.0f, DateTime.Now);

        // Entity should not move (blocked by constraint)
        Assert.Equal(initialPos, entity.Position);
    }

    [Fact]
    public void RemoveEntityFromPipe_MovesEntityOffPipe()
    {
        var j1 = CreateJunction(Vector3.Zero);
        var j2 = CreateJunction(new Vector3(100, 0, 0));

        var lane = new NavLane { Start = j1, End = j2, Length = 100.0f };
        var pipe = new Pipe
        {
            Id = 1,
            NavLanes = new List<NavLane> { lane },
            StartPosition = Vector3.Zero,
            EndPosition = new Vector3(100, 0, 0),
            Length = 100
        };

        var network = new PipeNetwork { Pipes = new List<Pipe> { pipe } };
        var controller = new PipeController(network);

        var entity = new MovingEntity { Id = 1, Route = new List<NavLane> { lane } };
        controller.PlaceEntity(entity);

        Assert.NotNull(entity.CurrentPipe);

        controller.RemoveEntityFromPipe(entity);

        Assert.Null(entity.CurrentPipe);
        Assert.Equal(0, pipe.CurrentOccupancy);
    }

    [Fact]
    public void GetEntity_ReturnsPlacedEntity()
    {
        var j1 = CreateJunction(Vector3.Zero);
        var j2 = CreateJunction(new Vector3(10, 0, 0));

        var lane = new NavLane { Start = j1, End = j2, Length = 10.0f };
        var pipe = new Pipe
        {
            Id = 1,
            NavLanes = new List<NavLane> { lane },
            StartPosition = Vector3.Zero,
            EndPosition = new Vector3(10, 0, 0),
            Length = 10
        };

        var network = new PipeNetwork { Pipes = new List<Pipe> { pipe } };
        var controller = new PipeController(network);

        var entity = new MovingEntity { Id = 42, Route = new List<NavLane> { lane } };
        controller.PlaceEntity(entity);

        var retrieved = controller.GetEntity(42);

        Assert.NotNull(retrieved);
        Assert.Equal(42, retrieved.Id);
    }

    [Fact]
    public void RegisterObstruction_CreatesSubdivision()
    {
        var j1 = CreateJunction(Vector3.Zero);
        var j2 = CreateJunction(new Vector3(100, 0, 0));

        var lane = new NavLane { Start = j1, End = j2, Length = 100.0f };
        var pipe = new Pipe
        {
            Id = 1,
            NavLanes = new List<NavLane> { lane },
            StartPosition = Vector3.Zero,
            EndPosition = new Vector3(100, 0, 0),
            Length = 100
        };

        var network = new PipeNetwork { Pipes = new List<Pipe> { pipe } };
        var controller = new PipeController(network);

        var obstaclePos = new Vector3(50, 0, 0);
        var duration = new CyclicConstraint
        {
            CycleSeconds = 120,
            ActivePhaseStart = 0,
            ActivePhaseDuration = 120
        };

        controller.RegisterObstruction(obstaclePos, 5.0f, duration, "Accident");

        Assert.True(pipe.HasSubdivisions);
        Assert.Single(pipe.Subdivisions);
    }

    [Fact]
    public void ReEnterPipe_AddsEntityBackToPipe()
    {
        var j1 = CreateJunction(Vector3.Zero);
        var j2 = CreateJunction(new Vector3(100, 0, 0));

        var lane = new NavLane { Start = j1, End = j2, Length = 100.0f };
        var pipe = new Pipe
        {
            Id = 1,
            NavLanes = new List<NavLane> { lane },
            StartPosition = Vector3.Zero,
            EndPosition = new Vector3(100, 0, 0),
            Length = 100
        };

        var network = new PipeNetwork { Pipes = new List<Pipe> { pipe } };
        var controller = new PipeController(network);

        var entity = new MovingEntity { Id = 1, Route = new List<NavLane> { lane } };
        controller.RemoveEntityFromPipe(entity);

        Assert.Null(entity.CurrentPipe);
        Assert.Contains(entity, controller.GetOffPipeEntities());

        controller.ReEnterPipe(entity, new Vector3(50, 0, 0));

        Assert.NotNull(entity.CurrentPipe);
        Assert.DoesNotContain(entity, controller.GetOffPipeEntities());
    }

    [Fact]
    public void GetOffPipeEntities_ReturnsRemovedEntities()
    {
        var j1 = CreateJunction(Vector3.Zero);
        var j2 = CreateJunction(new Vector3(100, 0, 0));

        var lane = new NavLane { Start = j1, End = j2, Length = 100.0f };
        var pipe = new Pipe
        {
            Id = 1,
            NavLanes = new List<NavLane> { lane },
            StartPosition = Vector3.Zero,
            EndPosition = new Vector3(100, 0, 0),
            Length = 100
        };

        var network = new PipeNetwork { Pipes = new List<Pipe> { pipe } };
        var controller = new PipeController(network);

        var entity1 = new MovingEntity { Id = 1, Route = new List<NavLane> { lane } };
        var entity2 = new MovingEntity { Id = 2, Route = new List<NavLane> { lane } };

        controller.PlaceEntity(entity1);
        controller.PlaceEntity(entity2);

        controller.RemoveEntityFromPipe(entity1);

        var offPipe = controller.GetOffPipeEntities().ToList();
        Assert.Single(offPipe);
        Assert.Contains(entity1, offPipe);
        Assert.DoesNotContain(entity2, offPipe);
    }
}
