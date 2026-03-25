using System;
using System.Numerics;
using Xunit;
using engine.navigation;

namespace JoyceCode.Tests.engine.navigation;

public class SpeedFunctionsTests
{
    [Fact]
    public void BrakingWave_SpeedDecreases_TowardObstacle()
    {
        var obstaclePos = new Vector3(50, 0, 0);
        var brakeFunc = SpeedFunctions.BrakingWave(obstaclePos, DateTime.Now, 10.0f);

        var farSpeed = brakeFunc(new Vector3(100, 0, 0), DateTime.Now);
        var nearSpeed = brakeFunc(new Vector3(30, 0, 0), DateTime.Now);

        Assert.True(farSpeed > nearSpeed);
        Assert.Equal(10.0f, farSpeed);  // Far: normal speed
        Assert.Equal(2.0f, nearSpeed);  // Near: 20% of normal
    }

    [Fact]
    public void BrakingWave_VeryCloseToObstacle_StopsCompletely()
    {
        var obstaclePos = new Vector3(50, 0, 0);
        var brakeFunc = SpeedFunctions.BrakingWave(obstaclePos, DateTime.Now, 10.0f);

        var veryCloseSpeed = brakeFunc(new Vector3(52, 0, 0), DateTime.Now);

        Assert.Equal(0.0f, veryCloseSpeed);
    }

    [Fact]
    public void AccelerationWave_SpeedIncreases_AfterClear()
    {
        var clearedPos = new Vector3(50, 0, 0);
        var startTime = DateTime.Now;
        var accelFunc = SpeedFunctions.AccelerationWave(clearedPos, startTime, 10.0f);

        var t0 = startTime.AddSeconds(0);
        var t5 = startTime.AddSeconds(5);

        var speed0 = accelFunc(new Vector3(50, 0, 0), t0);
        var speed5 = accelFunc(new Vector3(50, 0, 0), t5);

        Assert.True(speed5 > speed0);
    }

    [Fact]
    public void Congested_ReducesSpeed()
    {
        var congestedFunc = SpeedFunctions.Congested(congestionLevel: 0.5f, normalSpeed: 10.0f);

        var speed = congestedFunc(Vector3.Zero, DateTime.Now);

        Assert.Equal(5.0f, speed);  // 10.0f * (1.0f - 0.5f)
    }

    [Fact]
    public void Queued_BlockedReturnsZero()
    {
        var queueFunc = SpeedFunctions.Queued(Vector3.Zero, isBlocked: true);

        var speed = queueFunc(Vector3.Zero, DateTime.Now);

        Assert.Equal(0.0f, speed);
    }

    [Fact]
    public void Queued_NotBlockedReturnsFiveMS()
    {
        var queueFunc = SpeedFunctions.Queued(Vector3.Zero, isBlocked: false);

        var speed = queueFunc(Vector3.Zero, DateTime.Now);

        Assert.Equal(5.0f, speed);
    }

    [Fact]
    public void GradualSlowdown_FarFromTarget_ReturnsMaxSpeed()
    {
        var targetPos = new Vector3(50, 0, 0);
        var slowdownFunc = SpeedFunctions.GradualSlowdown(targetPos, slowdownDistance: 50.0f, minSpeed: 1.0f, maxSpeed: 10.0f);

        var farSpeed = slowdownFunc(new Vector3(150, 0, 0), DateTime.Now);

        Assert.Equal(10.0f, farSpeed, precision: 0.1f);  // Max speed far away
    }

    [Fact]
    public void GradualSlowdown_AtTarget_ReturnsMinSpeed()
    {
        var targetPos = new Vector3(50, 0, 0);
        var slowdownFunc = SpeedFunctions.GradualSlowdown(targetPos, slowdownDistance: 50.0f, minSpeed: 1.0f, maxSpeed: 10.0f);

        var targetSpeed = slowdownFunc(targetPos, DateTime.Now);

        Assert.Equal(1.0f, targetSpeed, precision: 0.1f);  // Min speed at target
    }
}
