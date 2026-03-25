using System;
using System.Collections.Generic;
using System.Numerics;
using Xunit;
using engine.tale;
using engine.navigation;
using builtin.modules.satnav.desc;

namespace JoyceCode.Tests.engine.tale;

public class RoutingPreferencesTests
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
    public void Fast_Goal_ReturnsUnityMultiplier()
    {
        var prefs = new RoutingPreferences { Goal = NpcGoal.Fast };
        var j1 = CreateJunction(Vector3.Zero);
        var j2 = CreateJunction(new Vector3(10, 0, 0));
        var lane = new NavLane { Start = j1, End = j2, Length = 10.0f };

        var multiplier = prefs.ComputeCostMultiplier(lane, TransportationType.Pedestrian);

        Assert.Equal(1.0f, multiplier);
    }

    [Fact]
    public void OnTime_Goal_PenalizeBlockedLanes()
    {
        var constraint = new CyclicConstraint
        {
            CycleSeconds = 60,
            ActivePhaseStart = 60,  // Always red
            ActivePhaseDuration = 0
        };

        var j1 = CreateJunction(Vector3.Zero);
        var j2 = CreateJunction(new Vector3(10, 0, 0));
        var lane = new NavLane
        {
            Start = j1,
            End = j2,
            Length = 10.0f,
            Constraint = constraint,
            AllowedTypes = new TransportationTypeFlags(TransportationType.Pedestrian)
        };

        var prefs = new RoutingPreferences
        {
            Goal = NpcGoal.OnTime,
            DeadlineTime = DateTime.Now.AddMinutes(5),
            Urgency = 1.0f  // Very urgent
        };

        var multiplier = prefs.ComputeCostMultiplier(lane, TransportationType.Pedestrian);

        Assert.True(multiplier > 1.0f);  // Penalized
    }

    [Fact]
    public void UpdateUrgency_Late_ReturnsMaxUrgency()
    {
        var prefs = new RoutingPreferences
        {
            DeadlineTime = DateTime.Now.AddMinutes(-5)  // 5 min ago
        };

        prefs.UpdateUrgency(DateTime.Now);

        Assert.Equal(1.0f, prefs.Urgency);
    }

    [Fact]
    public void UpdateUrgency_VeryCloseDeadline_ReturnsHighUrgency()
    {
        var prefs = new RoutingPreferences
        {
            DeadlineTime = DateTime.Now.AddMinutes(3)  // 3 min away
        };

        prefs.UpdateUrgency(DateTime.Now);

        Assert.True(prefs.Urgency > 0.8f);
    }

    [Fact]
    public void UpdateUrgency_FarDeadline_ReturnsLowUrgency()
    {
        var prefs = new RoutingPreferences
        {
            DeadlineTime = DateTime.Now.AddHours(2)  // 2 hours away
        };

        prefs.UpdateUrgency(DateTime.Now);

        Assert.True(prefs.Urgency < 0.3f);
    }

    [Fact]
    public void IsLate_BeforeDeadline_ReturnsFalse()
    {
        var prefs = new RoutingPreferences
        {
            DeadlineTime = DateTime.Now.AddMinutes(10)
        };

        Assert.False(prefs.IsLate);
    }

    [Fact]
    public void IsLate_AfterDeadline_ReturnsTrue()
    {
        var prefs = new RoutingPreferences
        {
            DeadlineTime = DateTime.Now.AddMinutes(-5)
        };

        Assert.True(prefs.IsLate);
    }

    [Fact]
    public void Scenic_Goal_ReturnsNeutralMultiplier()
    {
        var prefs = new RoutingPreferences { Goal = NpcGoal.Scenic };
        var j1 = CreateJunction(Vector3.Zero);
        var j2 = CreateJunction(new Vector3(10, 0, 0));
        var lane = new NavLane { Start = j1, End = j2, Length = 10.0f };

        var multiplier = prefs.ComputeCostMultiplier(lane, TransportationType.Pedestrian);

        Assert.Equal(1.0f, multiplier);  // Currently neutral (no scenic scoring implemented)
    }

    [Fact]
    public void Safe_Goal_ReturnsNeutralMultiplier()
    {
        var prefs = new RoutingPreferences { Goal = NpcGoal.Safe };
        var j1 = CreateJunction(Vector3.Zero);
        var j2 = CreateJunction(new Vector3(10, 0, 0));
        var lane = new NavLane { Start = j1, End = j2, Length = 10.0f };

        var multiplier = prefs.ComputeCostMultiplier(lane, TransportationType.Pedestrian);

        Assert.Equal(1.0f, multiplier);  // Currently neutral (no traffic density tracking)
    }

    [Fact]
    public void NoDeadline_OnTimeGoal_ReturnsUnityMultiplier()
    {
        var j1 = CreateJunction(Vector3.Zero);
        var j2 = CreateJunction(new Vector3(10, 0, 0));
        var lane = new NavLane { Start = j1, End = j2, Length = 10.0f };

        var prefs = new RoutingPreferences
        {
            Goal = NpcGoal.OnTime,
            DeadlineTime = null  // No deadline
        };

        var multiplier = prefs.ComputeCostMultiplier(lane, TransportationType.Pedestrian);

        Assert.Equal(1.0f, multiplier);  // No adjustment without deadline
    }
}
