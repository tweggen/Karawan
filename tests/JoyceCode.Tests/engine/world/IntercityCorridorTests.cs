using System;
using System.IO;
using System.Linq;
using System.Numerics;
using engine.world;
using Xunit;
using Xunit.Abstractions;

namespace JoyceCode.Tests.engine.world;


/**
 * KAR-483: the intercity corridors are highways - three times the ship's thrust, and with
 * it its cruising speed, plus a lane-keeping assist that yields to the player.
 *
 * Why thrust: the ship accelerates at LinearThrust x frontMotion/256 - LinearDamping x v
 * (HoverController), so at full throttle it settles at 15 x 255/256 / 0.6 = 24.9 m/s and
 * the 220 m/s limiter is never reached. Three times the thrust is three times the
 * acceleration AND three times the cruising speed, ~75 m/s.
 */
public class IntercityCorridorTests
{
    private readonly ITestOutputHelper _out;
    public IntercityCorridorTests(ITestOutputHelper output) => _out = output;

    private static IntercityCorridor.Line _line(Vector2 a, Vector2 b, float h = 100f) => new(a, b, h);

    // A west->east corridor, 5 km long, track at 100 m.
    private static readonly IntercityCorridor.Line[] One = { _line(new Vector2(0, 0), new Vector2(5000, 0)) };


    [Fact]
    public void TheBandIsWhatTheElevationOperatorFlattens()
    {
        // 2 x (FragmentSize / GroundResolution) = 2 x 20 m either side of the axis.
        Assert.Equal(40f, IntercityCorridor.HalfWidth);
        Assert.Equal(20f, IntercityCorridor.LaneOffset);
    }


    [Fact]
    public void APositionIsInTheCorridorOnlyBetweenItsStationsInItsBandNearItsTrack()
    {
        Assert.True(IntercityCorridor.TryLocate(new Vector3(2500, 103, 10), One, out var p));
        Assert.Equal(2500f, p.Along, 3);
        Assert.Equal(5000f, p.Length, 3);
        Assert.Equal(3f, p.HeightAboveTrack, 3);

        Assert.False(IntercityCorridor.TryLocate(new Vector3(-10, 103, 0), One, out _));     // before A
        Assert.False(IntercityCorridor.TryLocate(new Vector3(5010, 103, 0), One, out _));    // past B
        Assert.False(IntercityCorridor.TryLocate(new Vector3(2500, 103, 45), One, out _));   // off the band
        Assert.False(IntercityCorridor.TryLocate(new Vector3(2500, 130, 0), One, out _));    // high above it
        Assert.False(IntercityCorridor.TryLocate(new Vector3(2500, 103, 0), null, out _));
    }


    [Fact]
    public void LateralIsMeasuredToTheRightOfTheAxis()
    {
        // Facing -Z, right is +X: the engine's frame (Y up, right handed).
        Assert.Equal(new Vector2(1, 0), IntercityCorridor.RightOf(new Vector2(0, -1)));
        // West->east (+X): right is +Z.
        Assert.True(IntercityCorridor.TryLocate(new Vector3(2500, 103, 10), One, out var p));
        Assert.Equal(10f, p.Lateral, 3);
    }


    /**
     * Over the shipped world: every line's middle is in its own corridor, and no city's
     * centre is in any - a corridor is between cities, never through one.
     */
    [Fact]
    public void TheShippedWorldsCorridorsRunBetweenCities()
    {
        var harnessLines = IntercityWorldHarness.Lines();
        var lines = harnessLines.Select(l => new IntercityCorridor.Line(
            new Vector2(l.PosA.X, l.PosA.Z), new Vector2(l.PosB.X, l.PosB.Z), l.TrackHeight)).ToList();
        Assert.True(lines.Count > 100, $"{lines.Count} lines");

        for (int i = 0; i < lines.Count; ++i)
        {
            var mid = (lines[i].A + lines[i].B) / 2f;
            Assert.True(IntercityCorridor.TryLocate(new Vector3(mid.X, lines[i].TrackHeight + 3f, mid.Y), lines, out var p));
            Assert.Equal(i, p.LineIndex);
        }

        int citiesInACorridor = IntercityWorldHarness.Clusters().Count(cd =>
            IntercityCorridor.TryLocate(cd.Pos with { Y = cd.AverageHeight + 3f }, lines, out _));
        _out.WriteLine($"{lines.Count} corridors; city centres inside one: {citiesInACorridor}");
        Assert.Equal(0, citiesInACorridor);
    }


    [Fact]
    public void TheBoostRampsUpFromEachStation()
    {
        Assert.Equal(1f, IntercityCorridor.TargetBoost(null));
        IntercityCorridor.TryLocate(new Vector3(2500, 103, 0), One, out var middle);
        Assert.Equal(3f, IntercityCorridor.TargetBoost(middle), 4);

        IntercityCorridor.TryLocate(new Vector3(0.5f, 103, 0), One, out var atStation);
        Assert.InRange(IntercityCorridor.TargetBoost(atStation), 1f, 1.01f);

        float last = 1f;
        for (float x = 0f; x <= IntercityCorridor.RampMetres; x += 10f)
        {
            IntercityCorridor.TryLocate(new Vector3(x, 103, 0), One, out var p);
            float b = IntercityCorridor.TargetBoost(p);
            Assert.True(b >= last - 1e-5f);
            last = b;
        }
    }


    [Fact]
    public void LeavingRunsTheBoostOutGraduallyAndEnteringBuildsItAtOnce()
    {
        Assert.Equal(2.5f, IntercityCorridor.NextBoost(1f, 2.5f, 1f / 60f));

        float b = 3f;
        int frames = 0;
        while (b > 1f && frames < 10000)
        {
            b = IntercityCorridor.NextBoost(b, 1f, 1f / 60f);
            ++frames;
        }
        // From 3 to 1 at 0.5 per second: four seconds, not one frame.
        Assert.InRange(frames / 60f, 3.9f, 4.1f);
    }


    [Fact]
    public void TheAssistSteersTowardsTheLane_AndThatLaneIsOnTheRightOfTravel()
    {
        IntercityCorridor.TryLocate(new Vector3(2500, 103, 0), One, out var onAxis);
        var east = new Vector2(1, 0);
        var west = new Vector2(-1, 0);

        Assert.Equal(20f, IntercityCorridor.LaneCentreOf(onAxis, east));    // right of travel: +Z
        Assert.Equal(-20f, IntercityCorridor.LaneCentreOf(onAxis, west));

        // On the axis, heading east: the lane is to the right, so turn right (positive).
        Assert.True(IntercityCorridor.AssistTurn(onAxis, east, 75f, 0f, 1f / 60f) > 0f);
        // Heading west, the lane is at -Z, which is right of west too.
        Assert.True(IntercityCorridor.AssistTurn(onAxis, west, 75f, 0f, 1f / 60f) > 0f);

        IntercityCorridor.TryLocate(new Vector3(2500, 103, 20), One, out var inLane);
        Assert.InRange(IntercityCorridor.AssistTurn(inLane, east, 75f, 0f, 1f / 60f), -1e-5f, 1e-5f);

        IntercityCorridor.TryLocate(new Vector3(2500, 103, 35), One, out var tooFarRight);
        Assert.True(IntercityCorridor.AssistTurn(tooFarRight, east, 75f, 0f, 1f / 60f) < 0f);
    }


    [Fact]
    public void TheAssistYieldsAndIsBounded()
    {
        IntercityCorridor.TryLocate(new Vector3(2500, 103, -35), One, out var far);
        var east = new Vector2(1, 0);
        float dt = 1f / 60f;

        Assert.Equal(0f, IntercityCorridor.AssistTurn(far, east, 75f, 1f, dt));          // full steering
        Assert.Equal(0f, IntercityCorridor.AssistTurn(far, east, 3f, 0f, dt));           // parked
        Assert.Equal(0f, IntercityCorridor.AssistTurn(far, new Vector2(0, 1), 75f, 0f, dt)); // across
        float half = IntercityCorridor.AssistTurn(far, east, 75f, 0.5f, dt);
        float none = IntercityCorridor.AssistTurn(far, east, 75f, 0f, dt);
        Assert.Equal(none / 2f, half, 5);
        Assert.True(MathF.Abs(none) <= IntercityCorridor.MaxAssistTurnRate * dt + 1e-6f);
    }


    /**
     * Closed loop: a ship let go hands-off in the wrong half at 75 m/s settles into its
     * own lane and stays there, rather than oscillating out of the band.
     */
    [Fact]
    public void HandsOffTheShipSettlesIntoItsLane()
    {
        var pos = new Vector2(1000, -30);
        var front = Vector2.Normalize(new Vector2(1, 0.05f));
        float dt = 1f / 60f, speed = 75f, worst = 0f;

        for (int frame = 0; frame < 60 * 40; ++frame)
        {
            Assert.True(IntercityCorridor.TryLocate(new Vector3(pos.X, 103, pos.Y), One, out var p),
                $"left the corridor at {pos}");
            float a = IntercityCorridor.AssistTurn(p, front, speed, 0f, dt);
            var right = IntercityCorridor.RightOf(front);
            front = Vector2.Normalize(front * MathF.Cos(a) + right * MathF.Sin(a));
            pos += front * speed * dt;
            if (frame > 60 * 25) worst = MathF.Max(worst, MathF.Abs(p.Lateral - 20f));
        }

        _out.WriteLine($"after 25 s: lane error at most {worst:F2} m");
        Assert.True(worst < 1.5f, $"{worst} m off the lane centre after settling");
        Assert.True(Vector2.Dot(front, new Vector2(1, 0)) > 0.999f);
    }


    [Fact]
    public void TheShipTakesTheBoostAndTheAssist()
    {
        string root = Path.GetFullPath(Path.Combine(global::engine.GameRoot.PathTo("JoyceCode"), ".."));
        string hover = File.ReadAllText(Path.Combine(root, "nogameCode/nogame/modules/playerhover/HoverController.cs"));
        string scores = File.ReadAllText(Path.Combine(root, "nogameCode/nogame/modules/osd/Scores.cs"));

        Assert.Contains("vTotalImpulse += LinearThrust * _boost * vFront * frontMotion / 256f;", hover);
        Assert.Contains("IntercityCorridor.NextBoost(", hover);
        Assert.Contains("IntercityCorridor.AssistTurn(", hover);
        Assert.Contains("PlayerVehicleSpeed.Boost = _boost;", hover);
        Assert.Contains("PlayerVehicleSpeed.Boost", scores);
    }
}
