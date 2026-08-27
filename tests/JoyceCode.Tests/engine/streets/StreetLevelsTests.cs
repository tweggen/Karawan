using System;
using System.Numerics;
using engine.streets;
using engine.streets.generation;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * The mapping from deck level to height above ground.
 *
 * The property everything else leans on is that level 0 elevates by exactly nothing:
 * it is what makes every downstream elevation change a provable no-op while clusters
 * are ground-only, which in turn is what lets WP-5 be done incrementally without the
 * determinism baselines moving.
 */
public class StreetLevelsTests
{
    [Fact]
    public void TheGroundIsNotElevated()
    {
        Assert.Equal(0f, StreetLevels.ElevationOf(0));
    }


    [Fact]
    public void DecksStackEvenlyAboveAndBelow()
    {
        Assert.Equal(StreetLevels.DeckHeight, StreetLevels.ElevationOf(1));
        Assert.Equal(2f * StreetLevels.DeckHeight, StreetLevels.ElevationOf(2));
        Assert.Equal(-StreetLevels.DeckHeight, StreetLevels.ElevationOf(-1));
    }


    /**
     * A street has to fit underneath a deck, or the overpass is decorative.
     */
    [Fact]
    public void ADeckClearsTheStreetBelowIt()
    {
        Assert.True(StreetLevels.DeckHeight >= 4f,
            $"a deck {StreetLevels.DeckHeight} m up does not clear the traffic under it");
    }


    [Fact]
    public void AJunctionReportsTheElevationOfItsDeck()
    {
        var ground = new StreetPoint() { ClusterId = 0, Level = 0 };
        var raised = new StreetPoint() { ClusterId = 0, Level = 1 };

        Assert.Equal(0f, ground.LevelElevation);
        Assert.Equal(StreetLevels.DeckHeight, raised.LevelElevation);
    }


    /**
     * The property routing leans on.
     *
     * GenerateNavMapOperator gives each junction its deck height and then measures
     * lanes with Vector3.Distance, so a ramp is charged for climbing. If it were
     * measured in plan instead, a route over a bridge would look cheaper than it is and
     * traffic would prefer ramps to the flat street beside them.
     *
     * The operator itself needs a booted engine to run, so this pins the arithmetic it
     * performs rather than the operator.
     */
    [Fact]
    public void ARampIsChargedForItsSlopedLengthNotItsPlanLength()
    {
        var from = new StreetPoint() { ClusterId = 0, Level = 0 };
        from.SetPos(0f, 0f);
        var to = new StreetPoint() { ClusterId = 0, Level = 0 };
        to.SetPos(200f, 0f);

        var chain = new OverpassBuilder(0).Build(
            from, to, StrokeKind.Bridge, rampFraction: 0.25f, weight: 1f);
        var ramp = chain[0];

        float planLength = Vector2.Distance(ramp.A.Pos, ramp.B.Pos);

        Vector3 worldA = ramp.A.Pos3 with { Y = ramp.A.LevelElevation };
        Vector3 worldB = ramp.B.Pos3 with { Y = ramp.B.LevelElevation };
        float slopedLength = Vector3.Distance(worldA, worldB);

        Assert.True(slopedLength > planLength,
            $"a ramp climbing {StreetLevels.DeckHeight} m must be longer than its "
            + $"{planLength} m plan length, got {slopedLength}");

        Assert.Equal(
            MathF.Sqrt(planLength * planLength + StreetLevels.DeckHeight * StreetLevels.DeckHeight),
            slopedLength, 2);
    }


    /**
     * A flat street is unaffected: its sloped length is its plan length, which is why
     * elevating navigation leaves every ground-only route exactly as it was.
     */
    [Fact]
    public void AGroundStreetIsUnaffectedByTheSameArithmetic()
    {
        var a = new StreetPoint() { ClusterId = 0, Level = 0 };
        a.SetPos(0f, 0f);
        var b = new StreetPoint() { ClusterId = 0, Level = 0 };
        b.SetPos(120f, 50f);

        Assert.Equal(
            Vector2.Distance(a.Pos, b.Pos),
            Vector3.Distance(
                a.Pos3 with { Y = a.LevelElevation },
                b.Pos3 with { Y = b.LevelElevation }),
            3);
    }


    /**
     * Pos3 is the octree key, not a world position. If it ever starts carrying the
     * deck height, every neighbourhood query in StrokeStore silently becomes
     * three-dimensional - see the comment on StreetLevels.
     */
    [Fact]
    public void Pos3StaysPlanarEvenForARaisedJunction()
    {
        var raised = new StreetPoint() { ClusterId = 0, Level = 3 };
        raised.SetPos(12f, 34f);

        Assert.Equal(0f, raised.Pos3.Y);
        Assert.Equal(12f, raised.Pos3.X, 3);
        Assert.Equal(34f, raised.Pos3.Z, 3);
    }
}
