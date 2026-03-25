using System;
using System.Collections.Generic;
using System.Numerics;
using Xunit;
using builtin.modules.satnav.desc;
using engine.navigation;

namespace JoyceCode.Tests.engine.navigation;

public class RoutingGraphBuilderTests
{
    private NavJunction CreateTestJunction(Vector3 position)
    {
        return new NavJunction
        {
            Position = position,
            StartingLanes = new(),
            EndingLanes = new()
        };
    }

    [Fact]
    public void BuildFor_FilterLanesByType()
    {
        var navMap = new NavMap();

        // Create test lanes
        var j1 = CreateTestJunction(Vector3.Zero);
        var j2 = CreateTestJunction(new Vector3(10, 0, 0));
        var j3 = CreateTestJunction(new Vector3(20, 0, 0));

        var lane1 = new NavLane
        {
            Start = j1,
            End = j2,
            Length = 10.0f,
            MaxSpeed = 5.0f,
            AllowedTypes = new TransportationTypeFlags(TransportationType.Pedestrian)
        };

        var lane2 = new NavLane
        {
            Start = j2,
            End = j3,
            Length = 10.0f,
            MaxSpeed = 5.0f,
            AllowedTypes = new TransportationTypeFlags(
                TransportationType.Car | TransportationType.Pedestrian)
        };

        var lane3 = new NavLane
        {
            Start = j3,
            End = j1,
            Length = 20.0f,
            MaxSpeed = 10.0f,
            AllowedTypes = new TransportationTypeFlags(TransportationType.Car)
        };

        var allLanes = new List<NavLane> { lane1, lane2, lane3 };

        var builder = new RoutingGraphBuilder(navMap);

        // Build pedestrian graph
        var pedGraph = builder.BuildFor(TransportationType.Pedestrian, allLanes);

        // Pedestrian graph should include lane1 and lane2
        Assert.Contains(lane1, pedGraph.AllLanes);
        Assert.Contains(lane2, pedGraph.AllLanes);
        Assert.DoesNotContain(lane3, pedGraph.AllLanes);
        Assert.Equal(2, pedGraph.AllLanes.Count);

        // Build car graph
        var carGraph = builder.BuildFor(TransportationType.Car, allLanes);

        // Car graph should include lane2 and lane3
        Assert.DoesNotContain(lane1, carGraph.AllLanes);
        Assert.Contains(lane2, carGraph.AllLanes);
        Assert.Contains(lane3, carGraph.AllLanes);
        Assert.Equal(2, carGraph.AllLanes.Count);
    }

    [Fact]
    public void BuildFor_ConstructsEdgesCorrectly()
    {
        var navMap = new NavMap();

        // Create connected lanes
        var j1 = CreateTestJunction(Vector3.Zero);
        var j2 = CreateTestJunction(new Vector3(10, 0, 0));
        var j3 = CreateTestJunction(new Vector3(20, 0, 0));

        var lane1 = new NavLane
        {
            Start = j1,
            End = j2,
            Length = 10.0f,
            MaxSpeed = 5.0f,
            AllowedTypes = new TransportationTypeFlags(TransportationType.Car)
        };

        var lane2 = new NavLane
        {
            Start = j2,
            End = j3,
            Length = 10.0f,
            MaxSpeed = 5.0f,
            AllowedTypes = new TransportationTypeFlags(TransportationType.Car)
        };

        var allLanes = new List<NavLane> { lane1, lane2 };

        var builder = new RoutingGraphBuilder(navMap);
        var graph = builder.BuildFor(TransportationType.Car, allLanes);

        // lane1 should have lane2 as an outgoing edge (lane1.End == lane2.Start)
        var neighbors = graph.GetNeighbors(lane1);
        Assert.Contains(lane2, neighbors);
    }

    [Fact]
    public void BuildFor_DoesNotCreateEdgeBetweenDisconnectedLanes()
    {
        var navMap = new NavMap();

        // Create disconnected lanes
        var j1 = CreateTestJunction(Vector3.Zero);
        var j2 = CreateTestJunction(new Vector3(10, 0, 0));
        var j3 = CreateTestJunction(new Vector3(20, 0, 0));
        var j4 = CreateTestJunction(new Vector3(30, 0, 0));

        var lane1 = new NavLane
        {
            Start = j1,
            End = j2,
            Length = 10.0f,
            MaxSpeed = 5.0f,
            AllowedTypes = new TransportationTypeFlags(TransportationType.Car)
        };

        var lane2 = new NavLane
        {
            Start = j3,
            End = j4,
            Length = 10.0f,
            MaxSpeed = 5.0f,
            AllowedTypes = new TransportationTypeFlags(TransportationType.Car)
        };

        var allLanes = new List<NavLane> { lane1, lane2 };

        var builder = new RoutingGraphBuilder(navMap);
        var graph = builder.BuildFor(TransportationType.Car, allLanes);

        // lane1 should NOT have lane2 as a neighbor (disconnected)
        var neighbors = graph.GetNeighbors(lane1);
        Assert.DoesNotContain(lane2, neighbors);
    }

    [Fact]
    public void BuildFor_CachesGraphs()
    {
        var navMap = new NavMap();

        var j1 = CreateTestJunction(Vector3.Zero);
        var j2 = CreateTestJunction(new Vector3(10, 0, 0));

        var lane = new NavLane
        {
            Start = j1,
            End = j2,
            Length = 10.0f,
            MaxSpeed = 5.0f,
            AllowedTypes = new TransportationTypeFlags(TransportationType.Car)
        };

        var allLanes = new List<NavLane> { lane };

        var builder = new RoutingGraphBuilder(navMap);

        // Build twice
        var graph1 = builder.BuildFor(TransportationType.Car, allLanes);
        var graph2 = builder.BuildFor(TransportationType.Car, allLanes);

        // Should return same instance
        Assert.Same(graph1, graph2);
    }

    [Fact]
    public void InvalidateCache_ClearsCachedGraphs()
    {
        var navMap = new NavMap();

        var j1 = CreateTestJunction(Vector3.Zero);
        var j2 = CreateTestJunction(new Vector3(10, 0, 0));

        var lane = new NavLane
        {
            Start = j1,
            End = j2,
            Length = 10.0f,
            MaxSpeed = 5.0f,
            AllowedTypes = new TransportationTypeFlags(TransportationType.Car)
        };

        var allLanes = new List<NavLane> { lane };

        var builder = new RoutingGraphBuilder(navMap);

        var graph1 = builder.BuildFor(TransportationType.Car, allLanes);
        builder.InvalidateCache();
        var graph2 = builder.BuildFor(TransportationType.Car, allLanes);

        // Should be different instances
        Assert.NotSame(graph1, graph2);
    }

    [Fact]
    public void RoutingGraph_ContainsLane_ReturnsTrueForIncludedLane()
    {
        var navMap = new NavMap();

        var j1 = CreateTestJunction(Vector3.Zero);
        var j2 = CreateTestJunction(new Vector3(10, 0, 0));

        var lane = new NavLane
        {
            Start = j1,
            End = j2,
            Length = 10.0f,
            MaxSpeed = 5.0f,
            AllowedTypes = new TransportationTypeFlags(TransportationType.Car)
        };

        var allLanes = new List<NavLane> { lane };

        var builder = new RoutingGraphBuilder(navMap);
        var graph = builder.BuildFor(TransportationType.Car, allLanes);

        Assert.True(graph.ContainsLane(lane));
    }

    [Fact]
    public void RoutingGraph_ContainsLane_ReturnsFalseForExcludedLane()
    {
        var navMap = new NavMap();

        var j1 = CreateTestJunction(Vector3.Zero);
        var j2 = CreateTestJunction(new Vector3(10, 0, 0));
        var j3 = CreateTestJunction(new Vector3(20, 0, 0));

        var lane1 = new NavLane
        {
            Start = j1,
            End = j2,
            Length = 10.0f,
            MaxSpeed = 5.0f,
            AllowedTypes = new TransportationTypeFlags(TransportationType.Car)
        };

        var lane2 = new NavLane
        {
            Start = j2,
            End = j3,
            Length = 10.0f,
            MaxSpeed = 5.0f,
            AllowedTypes = new TransportationTypeFlags(TransportationType.Pedestrian)
        };

        var allLanes = new List<NavLane> { lane1, lane2 };

        var builder = new RoutingGraphBuilder(navMap);
        var carGraph = builder.BuildFor(TransportationType.Car, allLanes);

        Assert.True(carGraph.ContainsLane(lane1));
        Assert.False(carGraph.ContainsLane(lane2));  // Pedestrian only
    }

    [Fact]
    public void BuildFor_MultipleTypesCreateSeparateGraphs()
    {
        var navMap = new NavMap();

        var j1 = CreateTestJunction(Vector3.Zero);
        var j2 = CreateTestJunction(new Vector3(10, 0, 0));
        var j3 = CreateTestJunction(new Vector3(20, 0, 0));

        var lane1 = new NavLane
        {
            Start = j1,
            End = j2,
            Length = 10.0f,
            MaxSpeed = 5.0f,
            AllowedTypes = new TransportationTypeFlags(
                TransportationType.Pedestrian | TransportationType.Car)
        };

        var lane2 = new NavLane
        {
            Start = j2,
            End = j3,
            Length = 10.0f,
            MaxSpeed = 5.0f,
            AllowedTypes = new TransportationTypeFlags(TransportationType.Pedestrian)
        };

        var allLanes = new List<NavLane> { lane1, lane2 };

        var builder = new RoutingGraphBuilder(navMap);

        var pedGraph = builder.BuildFor(TransportationType.Pedestrian, allLanes);
        var carGraph = builder.BuildFor(TransportationType.Car, allLanes);

        // Pedestrian graph should have both lanes
        Assert.Equal(2, pedGraph.AllLanes.Count);

        // Car graph should have only lane1
        Assert.Single(carGraph.AllLanes);
        Assert.Contains(lane1, carGraph.AllLanes);
        Assert.DoesNotContain(lane2, carGraph.AllLanes);
    }

    [Fact]
    public void GetCacheStats_ReturnsCorrectCounts()
    {
        var navMap = new NavMap();

        var j1 = CreateTestJunction(Vector3.Zero);
        var j2 = CreateTestJunction(new Vector3(10, 0, 0));

        var lane1 = new NavLane
        {
            Start = j1,
            End = j2,
            Length = 10.0f,
            MaxSpeed = 5.0f,
            AllowedTypes = new TransportationTypeFlags(TransportationType.Pedestrian)
        };

        var lane2 = new NavLane
        {
            Start = j1,
            End = j2,
            Length = 10.0f,
            MaxSpeed = 15.0f,
            AllowedTypes = new TransportationTypeFlags(TransportationType.Car)
        };

        var allLanes = new List<NavLane> { lane1, lane2 };

        var builder = new RoutingGraphBuilder(navMap);

        // Build graphs
        builder.BuildFor(TransportationType.Pedestrian, allLanes);
        builder.BuildFor(TransportationType.Car, allLanes);

        var stats = builder.GetCacheStats();

        Assert.Equal(2, stats.Count);
        Assert.Equal(1, stats[TransportationType.Pedestrian]);
        Assert.Equal(1, stats[TransportationType.Car]);
    }
}
