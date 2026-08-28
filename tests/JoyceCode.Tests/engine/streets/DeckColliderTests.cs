using System;
using System.Numerics;
using engine.streets;
using engine.streets.generation;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * The collider that stands in for a raised road.
 *
 * The Bepu call that consumes this needs a live simulation and is not covered here;
 * what is covered is the arithmetic, which is where a bridge you fall through would
 * actually come from.
 */
public class DeckColliderTests
{
    private const float Thickness = 0.1f;
    private const float Width = 12f;


    private static StreetPoint _pointAt(float x, float y, sbyte level)
    {
        var sp = new StreetPoint() { ClusterId = 0, Level = level };
        sp.SetPos(x, y);
        return sp;
    }


    private static Stroke _stroke(sbyte levelA, sbyte levelB)
    {
        var s = new Stroke() { ClusterId = 0 };
        s.A = _pointAt(0f, 0f, levelA);
        s.B = _pointAt(100f, 0f, levelB);
        return s;
    }


    /**
     * In a flat city, ground streets are covered by the fragment floor plane, so giving
     * them boxes would be pure cost.
     */
    [Fact]
    public void OnlyRaisedStrokesNeedAColliderInAFlatCity()
    {
        Assert.False(DeckCollider.IsNeededFor(_stroke(0, 0), groundIsFlat: true));
        Assert.True(DeckCollider.IsNeededFor(_stroke(1, 1), groundIsFlat: true));
        Assert.True(DeckCollider.IsNeededFor(_stroke(0, 1), groundIsFlat: true),
            "a ramp leaves the ground even though one end is on it");
    }


    /**
     * Once the city follows its terrain there is no height a floor plane could sit at,
     * so every street carries its own surface - including the ordinary ground ones that
     * needed nothing before.
     */
    [Fact]
    public void EveryStrokeNeedsAColliderOnceTheGroundIsNotFlat()
    {
        Assert.True(DeckCollider.IsNeededFor(_stroke(0, 0), groundIsFlat: false),
            "a hillside street has no floor plane under it");
        Assert.True(DeckCollider.IsNeededFor(_stroke(1, 1), groundIsFlat: false));
        Assert.True(DeckCollider.IsNeededFor(_stroke(0, 1), groundIsFlat: false));
    }


    /**
     * A flat deck: level, and its top face is the road.
     */
    [Fact]
    public void AFlatDeckLiesLevelWithItsTopFaceOnTheRoad()
    {
        var c = DeckCollider.For(
            new Vector3(0f, 10f, 0f), new Vector3(100f, 10f, 0f), Width, Thickness);

        Assert.Equal(100f, c.Length, 3);
        Assert.Equal(Width, c.Width, 3);

        /* centre sits half a thickness below the surface */
        Assert.Equal(10f - Thickness / 2f, c.Position.Y, 4);
        Assert.Equal(50f, c.Position.X, 3);

        Vector3 up = Vector3.Transform(Vector3.UnitY, c.Orientation);
        Assert.Equal(1f, up.Y, 4);
    }


    /**
     * A ramp: as long as the slope, tilted by it, and still lowered along its own
     * normal so the top face is the road rather than something a wheel clips.
     */
    [Fact]
    public void ARampColliderFollowsTheSlope()
    {
        Vector3 a = new(0f, 2f, 0f);
        Vector3 b = new(60f, 10f, 0f);

        var c = DeckCollider.For(a, b, Width, Thickness);

        Assert.Equal(Vector3.Distance(a, b), c.Length, 3);
        Assert.True(c.Length > 60f, "the collider is as long as the slope, not its shadow");

        /* local X runs along the ramp */
        Vector3 along = Vector3.Transform(Vector3.UnitX, c.Orientation);
        Assert.Equal(Vector3.Normalize(b - a).X, along.X, 3);
        Assert.Equal(Vector3.Normalize(b - a).Y, along.Y, 3);

        /* local Y is the surface normal: still upward, and leaning against the climb */
        Vector3 up = Vector3.Transform(Vector3.UnitY, c.Orientation);
        Assert.True(up.Y > 0f);
        Assert.True(up.X < 0f, $"normal should lean against the climb, got {up}");
        Assert.Equal(1f, up.Length(), 4);

        /* perpendicular, or the box is skewed */
        Assert.Equal(0f, Vector3.Dot(along, up), 4);

        /*
         * The centre is the midpoint dropped along the SURFACE NORMAL, not straight
         * down. On a slope those differ, and only the former keeps the whole top face
         * flush with the road - drop it straight down and one end of the slab pokes
         * through while the other sinks.
         *
         * Distance alone cannot tell the two apart, since both move by half a
         * thickness. The displacement has to be checked as a vector.
         */
        Vector3 midpoint = (a + b) * 0.5f;
        Vector3 displacement = midpoint - c.Position;

        Assert.Equal(Thickness / 2f, displacement.Length(), 4);
        Assert.Equal(up.X * (Thickness / 2f), displacement.X, 5);
        Assert.Equal(up.Y * (Thickness / 2f), displacement.Y, 5);
        Assert.True(Single.Abs(displacement.X) > 1e-4f,
            "on a slope the drop leans, so it is not purely vertical");
        Assert.True(c.Position.Y < midpoint.Y);
    }


    /**
     * A road banks along its length, never sideways, whatever direction it runs in.
     */
    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(100f, 100f)]
    [InlineData(-40f, 70f)]
    [InlineData(0f, -120f)]
    public void ARoadIsNeverBankedSideways(float bx, float bz)
    {
        if (bx == 0f && bz == 0f) return;

        var c = DeckCollider.For(
            new Vector3(0f, 2f, 0f), new Vector3(bx, 10f, bz), Width, Thickness);

        Vector3 across = Vector3.Transform(Vector3.UnitZ, c.Orientation);
        Assert.Equal(0f, across.Y, 4);
    }


    [Fact]
    public void ADegenerateStrokeYieldsNoUsableCollider()
    {
        var c = DeckCollider.For(
            new Vector3(5f, 10f, 5f), new Vector3(5f, 10f, 5f), Width, Thickness);

        Assert.Equal(0f, c.Length, 5);
    }
}
