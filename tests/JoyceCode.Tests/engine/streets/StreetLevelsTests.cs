using engine.streets;
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
