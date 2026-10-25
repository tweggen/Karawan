using engine.streets;
using engine.streets.generation;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * Which strokes get a collider of their own. Its shape is CarriagewayColliderTests'.
 */
public class DeckColliderTests
{
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
}
