using System;
using System.Numerics;
using Xunit;
using builtin.modules.satnav.desc;
using engine.navigation;

namespace JoyceCode.Tests.engine.navigation;

public class NavLaneTests
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
    public void NavLane_AllowedTypes_DefaultsPedestrian()
    {
        var start = CreateTestJunction(Vector3.Zero);
        var end = CreateTestJunction(new Vector3(10, 0, 0));

        var lane = new NavLane
        {
            Start = start,
            End = end,
            MaxSpeed = 5.0f,
            Length = 10.0f
        };

        Assert.True(lane.AllowedTypes.HasFlag(TransportationType.Pedestrian));
    }

    [Fact]
    public void NavLane_GetCost_ReturnsDifferentCostsForDifferentTypes()
    {
        var start = CreateTestJunction(Vector3.Zero);
        var end = CreateTestJunction(new Vector3(100, 0, 0));

        var lane = new NavLane
        {
            Start = start,
            End = end,
            MaxSpeed = 0,  // Use type-based speeds
            Length = 100.0f,
            AllowedTypes = new TransportationTypeFlags(
                TransportationType.Pedestrian | TransportationType.Car)
        };

        var pedestrianCost = lane.GetCost(TransportationType.Pedestrian);
        var carCost = lane.GetCost(TransportationType.Car);

        Assert.NotEqual(pedestrianCost, carCost);
        Assert.True(carCost < pedestrianCost);  // Cars faster
    }

    [Fact]
    public void NavLane_GetCost_ReturnsMaxValueForDisallowedType()
    {
        var start = CreateTestJunction(Vector3.Zero);
        var end = CreateTestJunction(new Vector3(100, 0, 0));

        var lane = new NavLane
        {
            Start = start,
            End = end,
            MaxSpeed = 5.0f,
            Length = 100.0f,
            AllowedTypes = new TransportationTypeFlags(TransportationType.Car)
        };

        var pedestrianCost = lane.GetCost(TransportationType.Pedestrian);

        Assert.Equal(float.MaxValue, pedestrianCost);
    }

    [Fact]
    public void NavLane_GetCost_PedestrianCostIsGreaterThanCarCost()
    {
        var start = CreateTestJunction(Vector3.Zero);
        var end = CreateTestJunction(new Vector3(100, 0, 0));

        var lane = new NavLane
        {
            Start = start,
            End = end,
            MaxSpeed = 0,
            Length = 100.0f,
            AllowedTypes = new TransportationTypeFlags(
                TransportationType.Pedestrian | TransportationType.Car)
        };

        var pedestrianCost = lane.GetCost(TransportationType.Pedestrian);
        var carCost = lane.GetCost(TransportationType.Car);

        // Pedestrian (1.5 m/s) should take longer than car (13.4 m/s)
        // Cost = distance / speed
        var expectedPedestrianCost = 100.0f / 1.5f;   // ~66.67 seconds
        var expectedCarCost = 100.0f / 13.4f;         // ~7.46 seconds

        Assert.Equal(expectedPedestrianCost, pedestrianCost, precision: 0.1f);
        Assert.Equal(expectedCarCost, carCost, precision: 0.1f);
    }

    [Fact]
    public void NavLane_GetCost_UsesMaxSpeedAsUpperBound()
    {
        var start = CreateTestJunction(Vector3.Zero);
        var end = CreateTestJunction(new Vector3(100, 0, 0));

        var lane = new NavLane
        {
            Start = start,
            End = end,
            MaxSpeed = 5.0f,  // Limit to 5 m/s
            Length = 100.0f,
            AllowedTypes = new TransportationTypeFlags(TransportationType.Car)
        };

        var carCost = lane.GetCost(TransportationType.Car);

        // Car speed is limited to MaxSpeed (5.0f)
        var expectedCost = 100.0f / 5.0f;  // 20 seconds
        Assert.Equal(expectedCost, carCost, precision: 0.1f);
    }

    [Fact]
    public void NavLane_GetCost_BicycleCost()
    {
        var start = CreateTestJunction(Vector3.Zero);
        var end = CreateTestJunction(new Vector3(100, 0, 0));

        var lane = new NavLane
        {
            Start = start,
            End = end,
            MaxSpeed = 0,
            Length = 100.0f,
            AllowedTypes = new TransportationTypeFlags(
                TransportationType.Bicycle | TransportationType.Car)
        };

        var bicycleCost = lane.GetCost(TransportationType.Bicycle);
        var carCost = lane.GetCost(TransportationType.Car);

        // Bicycle (5.0 m/s) should be faster than pedestrian, slower than car
        Assert.True(bicycleCost < 100.0f / 1.5f);  // Faster than pedestrian
        Assert.True(bicycleCost > carCost);        // Slower than car
    }

    [Fact]
    public void NavLane_GetCost_BusCost()
    {
        var start = CreateTestJunction(Vector3.Zero);
        var end = CreateTestJunction(new Vector3(100, 0, 0));

        var lane = new NavLane
        {
            Start = start,
            End = end,
            MaxSpeed = 0,
            Length = 100.0f,
            AllowedTypes = new TransportationTypeFlags(
                TransportationType.Bus | TransportationType.Car)
        };

        var busCost = lane.GetCost(TransportationType.Bus);
        var carCost = lane.GetCost(TransportationType.Car);

        // Bus (11.0 m/s) should be slower than typical car (13.4 m/s)
        Assert.True(busCost > carCost);
    }

    [Fact]
    public void NavLane_GetCost_ZeroLengthReturnsZero()
    {
        var start = CreateTestJunction(Vector3.Zero);
        var end = CreateTestJunction(Vector3.Zero);

        var lane = new NavLane
        {
            Start = start,
            End = end,
            MaxSpeed = 5.0f,
            Length = 0.0f,
            AllowedTypes = new TransportationTypeFlags(TransportationType.Pedestrian)
        };

        var cost = lane.GetCost(TransportationType.Pedestrian);

        Assert.Equal(0.0f, cost);
    }

    [Fact]
    public void NavLane_GetCost_NegativeLengthReturnsZero()
    {
        var start = CreateTestJunction(Vector3.Zero);
        var end = CreateTestJunction(new Vector3(100, 0, 0));

        var lane = new NavLane
        {
            Start = start,
            End = end,
            MaxSpeed = 5.0f,
            Length = -10.0f,
            AllowedTypes = new TransportationTypeFlags(TransportationType.Pedestrian)
        };

        var cost = lane.GetCost(TransportationType.Pedestrian);

        Assert.Equal(0.0f, cost);
    }

    [Fact]
    public void NavLane_AllowedTypes_CanBeModified()
    {
        var start = CreateTestJunction(Vector3.Zero);
        var end = CreateTestJunction(new Vector3(10, 0, 0));

        var lane = new NavLane
        {
            Start = start,
            End = end,
            MaxSpeed = 5.0f,
            Length = 10.0f
        };

        // Starts with Pedestrian
        Assert.True(lane.AllowedTypes.HasFlag(TransportationType.Pedestrian));
        Assert.False(lane.AllowedTypes.HasFlag(TransportationType.Car));

        // Add Car
        lane.AllowedTypes.Add(TransportationType.Car);
        Assert.True(lane.AllowedTypes.HasFlag(TransportationType.Car));

        // Remove Pedestrian
        lane.AllowedTypes.Remove(TransportationType.Pedestrian);
        Assert.False(lane.AllowedTypes.HasFlag(TransportationType.Pedestrian));
    }
}
