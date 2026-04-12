using System;
using Xunit;
using engine.navigation;

namespace JoyceCode.Tests.engine.navigation;

public class CyclicConstraintTests
{
    [Fact]
    public void Query_GreenPhase_ReturnsCanAccessTrue()
    {
        var constraint = new CyclicConstraint
        {
            CycleSeconds = 60,
            ActivePhaseStart = 0,
            ActivePhaseDuration = 30
        };

        var time = DateTime.UnixEpoch.AddSeconds(15);
        var state = constraint.Query(time);

        Assert.True(state.CanAccess);
    }

    [Fact]
    public void Query_RedPhase_ReturnsCanAccessFalse()
    {
        var constraint = new CyclicConstraint
        {
            CycleSeconds = 60,
            ActivePhaseStart = 0,
            ActivePhaseDuration = 30
        };

        var time = DateTime.UnixEpoch.AddSeconds(45);
        var state = constraint.Query(time);

        Assert.False(state.CanAccess);
    }

    [Fact]
    public void Query_ReturnsCorrectTimeUntilChange()
    {
        var constraint = new CyclicConstraint
        {
            CycleSeconds = 60,
            ActivePhaseStart = 0,
            ActivePhaseDuration = 30
        };

        // At t=15s: green until 30s = 15s remaining
        var time = DateTime.UnixEpoch.AddSeconds(15);
        var state = constraint.Query(time);

        Assert.Equal(15, state.UntilChange.TotalSeconds, precision: 0.1);
    }

    [Fact]
    public void Query_Cycle_RepeatsCorrectly()
    {
        var constraint = new CyclicConstraint
        {
            CycleSeconds = 60,
            ActivePhaseStart = 0,
            ActivePhaseDuration = 30
        };

        // t=15s: green
        // t=75s (15+60): also green (second cycle)
        var state1 = constraint.Query(DateTime.UnixEpoch.AddSeconds(15));
        var state2 = constraint.Query(DateTime.UnixEpoch.AddSeconds(75));

        Assert.True(state1.CanAccess);
        Assert.True(state2.CanAccess);
    }

    [Fact]
    public void Query_OffsetCycle_WorksCorrectly()
    {
        var constraint = new CyclicConstraint
        {
            CycleSeconds = 60,
            ActivePhaseStart = 20,      // Green starts at 20s
            ActivePhaseDuration = 20    // Green until 40s
        };

        var greenTime = DateTime.UnixEpoch.AddSeconds(30);
        var redTime = DateTime.UnixEpoch.AddSeconds(50);

        Assert.True(constraint.Query(greenTime).CanAccess);
        Assert.False(constraint.Query(redTime).CanAccess);
    }
}
