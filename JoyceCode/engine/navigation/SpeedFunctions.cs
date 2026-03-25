using System;
using System.Numerics;

namespace engine.navigation;

/// <summary>
/// Predefined speed functions for common scenarios.
/// These model realistic traffic behavior (braking waves, acceleration).
/// </summary>
public static class SpeedFunctions
{
    /// <summary>
    /// Braking wave when obstruction appears.
    /// Entities near obstacle brake hard, entities far brake gently.
    /// </summary>
    public static Func<Vector3, DateTime, float> BrakingWave(
        Vector3 obstaclePosition,
        DateTime brakeStartTime,
        float normalSpeed = 10.0f)
    {
        return (position, currentTime) =>
        {
            var distance = Vector3.Distance(position, obstaclePosition);

            // Distance zones
            if (distance < 5.0f)
                return 0.0f;      // Very close: stop
            if (distance < 20.0f)
                return normalSpeed * 0.2f;  // Near: slow
            if (distance < 50.0f)
                return normalSpeed * 0.5f;  // Medium: slower

            return normalSpeed;   // Far: normal
        };
    }

    /// <summary>
    /// Acceleration wave when obstruction clears.
    /// Entities near cleared area accelerate first, propagates backward.
    /// </summary>
    public static Func<Vector3, DateTime, float> AccelerationWave(
        Vector3 clearedPosition,
        DateTime accelerationStartTime,
        float normalSpeed = 10.0f,
        float accelerationRate = 2.0f)
    {
        return (position, currentTime) =>
        {
            var distance = Vector3.Distance(position, clearedPosition);
            var timeSinceClear = (currentTime - accelerationStartTime).TotalSeconds;

            if (timeSinceClear < 0)
                return normalSpeed * 0.2f;  // Before clear: still slow

            // Entities near cleared area accelerate first
            if (distance < 20.0f)
            {
                var speed = normalSpeed * 0.2f + accelerationRate * (float)timeSinceClear;
                return Math.Min(speed, normalSpeed);
            }

            // Entities farther away accelerate more gently
            var delayedStart = Math.Max(0, timeSinceClear - (distance / normalSpeed));
            var acceleratingSpeed = normalSpeed * 0.2f + accelerationRate * (float)delayedStart;
            return Math.Min(acceleratingSpeed, normalSpeed);
        };
    }

    /// <summary>
    /// Queue pattern: entities back up and wait.
    /// Used when pipe is blocked completely.
    /// </summary>
    public static Func<Vector3, DateTime, float> Queued(
        Vector3 queueStart,
        bool isBlocked = true)
    {
        return (position, currentTime) =>
        {
            return isBlocked ? 0.0f : 5.0f;
        };
    }

    /// <summary>
    /// Congestion: reduced speed due to traffic density.
    /// </summary>
    public static Func<Vector3, DateTime, float> Congested(
        float congestionLevel = 0.5f,  // 0 = free flow, 1 = completely congested
        float normalSpeed = 10.0f)
    {
        return (position, currentTime) =>
        {
            return normalSpeed * (1.0f - congestionLevel);
        };
    }

    /// <summary>
    /// Gradual slowdown over a distance zone.
    /// Useful for approach to destinations or gradual obstacles.
    /// </summary>
    public static Func<Vector3, DateTime, float> GradualSlowdown(
        Vector3 targetPosition,
        float slowdownDistance = 50.0f,
        float minSpeed = 1.0f,
        float maxSpeed = 10.0f)
    {
        return (position, currentTime) =>
        {
            var distance = Vector3.Distance(position, targetPosition);

            if (distance <= 0)
                return minSpeed;

            // Linear interpolation between minSpeed and maxSpeed based on distance
            var ratio = Math.Min(1.0f, distance / slowdownDistance);
            return minSpeed + (maxSpeed - minSpeed) * ratio;
        };
    }
}
