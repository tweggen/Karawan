using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using builtin.tools;
using engine.streets;
using engine.streets.generation;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * WP-B1.3: the orphan bridger picks its partner by PLAN distance.
 *
 * That is the right measure on the ground and the wrong one the moment a second deck
 * exists, because two junctions stacked one above the other are zero metres apart in
 * plan and eight metres apart in the world. Bridging to one of those would emit an
 * ordinary street joining two decks - the one thing the level model forbids - and now
 * that the pass goes through NetworkBuilder that is not a silently bad graph but a
 * throw in the middle of world generation. So the fix is in the CHOICE, not in the
 * commit: a junction on another deck is not a candidate at all.
 *
 * Every junction of a ground-only city is on level 0, so the filter admits exactly the
 * set it always did there - which the eight recorded fingerprints are what prove.
 */
public class ConnectComponentsLevelTests
{
    private const float ClusterSize = 2000f;


    private static StreetPoint _pointAt(float x, float y, sbyte level = 0)
    {
        var sp = new StreetPoint() { ClusterId = 0, Level = level };
        sp.SetPos(x, y);
        return sp;
    }


    private static Stroke _add(StrokeStore store,
        float x0, float y0, float x1, float y1, sbyte level = 0)
    {
        var s = new Stroke()
        {
            ClusterId = 0, IsPrimary = true, Weight = 1f, Level = level,
            Kind = StrokeKind.Street
        };
        s.A = _pointAt(x0, y0, level);
        s.B = _pointAt(x1, y1, level);
        store.AddStroke(s);
        return s;
    }


    /**
     * A connected polyline, sharing one junction between consecutive strokes. Building
     * each stroke from fresh endpoints instead would put two points on the same spot,
     * which StrokeStore.AddPoint refuses outright.
     */
    private static List<StreetPoint> _polyline(
        StrokeStore store, sbyte level, params (float x, float y)[] pts)
    {
        var points = pts.Select(p => _pointAt(p.x, p.y, level)).ToList();

        for (int i = 0; i + 1 < points.Count; ++i)
        {
            var s = new Stroke()
            {
                ClusterId = 0, IsPrimary = true, Weight = 1f, Level = level,
                Kind = StrokeKind.Street
            };
            s.A = points[i];
            s.B = points[i + 1];
            store.AddStroke(s);
        }

        return points;
    }


    private static void _run(StrokeStore store)
    {
        new ConnectComponentsPass(
            store, new NetworkBuilder(store), 0, new RandomSource("connect"), "test").Run();
    }


    private static List<Stroke> _connectors(StrokeStore store)
        => store.GetStrokes().Where(s => s.Kind == StrokeKind.ConnectorBridge).ToList();


    /**
     * The fixture the AC asks for: the nearest main junction IN PLAN is a deck junction
     * stacked over the orphan, and the nearest GROUND one is further away. The bridge
     * has to go to the ground one.
     *
     * Asserted by identity on the junction, not by distance: this geometry deliberately
     * puts two candidates close together, and a metric assertion could be satisfied by
     * the wrong one.
     */
    [Fact]
    public void TheBridgeSkipsADeckJunctionStackedOverTheOrphan()
    {
        var store = new StrokeStore(ClusterSize);

        /*
         * Main component on the ground, plus a deck carried above it. The deck's own
         * end lands at (60,0) - 60 m from the orphan - while the nearest ground
         * junction of the main component is at (0,0), 120 m away.
         */
        var mainGround = _add(store, -200f, 0f, 0f, 0f);
        var deck = _add(store, -200f, 60f, 60f, 0f, level: 1);

        /*
         * Deck and ground are one component: the ramp joins them.
         */
        var ramp = new Stroke()
        {
            ClusterId = 0, IsPrimary = true, Weight = 1f, Level = 0, Kind = StrokeKind.Ramp
        };
        ramp.A = mainGround.A;
        ramp.B = deck.A;
        store.AddStroke(ramp);

        /*
         * The orphan, on the ground, 60 m from the deck end and 120 m from (0,0).
         */
        var orphan = _add(store, 120f, 0f, 300f, 0f);

        Assert.True(Vector2.Distance(orphan.A.Pos, deck.B.Pos)
                    < Vector2.Distance(orphan.A.Pos, mainGround.B.Pos),
            "the fixture is supposed to put the deck junction nearer in plan");

        _run(store);

        var connectors = _connectors(store);
        Assert.Single(connectors);

        var bridge = connectors[0];
        Assert.Equal((sbyte)0, bridge.A.Level);
        Assert.Equal((sbyte)0, bridge.B.Level);

        /*
         * Identity, not proximity.
         */
        Assert.True(bridge.A == mainGround.B || bridge.B == mainGround.B,
            $"the bridge {bridge.A.Pos}..{bridge.B.Pos} does not touch the ground "
            + $"junction at {mainGround.B.Pos}");
        Assert.True(bridge.A != deck.B && bridge.B != deck.B,
            "the bridge was attached to the deck junction");
    }


    /**
     * The control: with the deck junction moved out of the way, the SAME run picks the
     * ground junction that is genuinely nearest. Without it the test above passes for a
     * pass that always picks (0,0) for some unrelated reason.
     */
    [Fact]
    public void AGroundJunctionThatIsNearerIsStillTheOneChosen()
    {
        var store = new StrokeStore(ClusterSize);

        var main = _polyline(store, 0, (-400f, 0f), (-300f, 0f), (-60f, 0f));
        StreetPoint nearest = main[2];
        _add(store, 120f, 0f, 300f, 0f);

        _run(store);

        var connectors = _connectors(store);
        Assert.Single(connectors);

        Assert.True(connectors[0].A == nearest || connectors[0].B == nearest,
            "the bridge did not attach to the nearest ground junction");
    }


    /**
     * The filter has to be on BOTH loops, and this is what says so.
     *
     * The first loop decides which junction of the orphan the bridge leaves from, by
     * how near the main component comes to it; the second picks the partner. Dropping
     * the filter from the first alone still produces a level-correct bridge - the
     * second loop refuses the deck junction - but from the WRONG END of the orphan,
     * because a deck junction stacked over one end made that end look nearest.
     *
     * Here the orphan's far corner has a deck junction 10 m above it and no ground
     * junction within 400 m, while its near corner has one at 200 m.
     */
    [Fact]
    public void TheOrphanEndTheBridgeLeavesFromIgnoresDeckJunctionsToo()
    {
        var store = new StrokeStore(ClusterSize);

        var ground = _polyline(store, 0, (-600f, 0f), (100f, 0f));
        var deck = _polyline(store, 1, (-600f, 100f), (510f, 200f));

        var ramp = new Stroke()
        {
            ClusterId = 0, IsPrimary = true, Weight = 1f, Level = 0, Kind = StrokeKind.Ramp
        };
        ramp.A = ground[0];
        ramp.B = deck[0];
        store.AddStroke(ramp);

        var orphan = _polyline(store, 0, (300f, 0f), (500f, 0f), (500f, 200f));
        StreetPoint nearEnd = orphan[0];
        StreetPoint farCorner = orphan[2];

        Assert.True(Vector2.Distance(farCorner.Pos, deck[1].Pos)
                    < Vector2.Distance(nearEnd.Pos, ground[1].Pos),
            "the fixture is supposed to make the far corner look nearest in plan");

        _run(store);

        var connectors = _connectors(store);
        Assert.Single(connectors);

        Assert.True(connectors[0].A == nearEnd || connectors[0].B == nearEnd,
            $"the bridge left the orphan at {connectors[0].A.Pos}..{connectors[0].B.Pos} "
            + $"rather than from its near end at {nearEnd.Pos}");
    }


    /**
     * A whole component on its own deck bridges to a junction on that deck, not to the
     * ground under it. This is the same defect from the other side: the ground is what
     * is nearest in plan here.
     */
    [Fact]
    public void ADeckOrphanBridgesAlongItsOwnDeck()
    {
        var store = new StrokeStore(ClusterSize);

        /*
         * Main component: ground, plus a deck stub reachable through a ramp. The deck
         * stub ends at (-60,200); the ground reaches (-60,0).
         */
        var mainGround = _add(store, -400f, 0f, -60f, 0f);
        var deckStub = _add(store, -400f, 200f, -60f, 200f, level: 1);
        var ramp = new Stroke()
        {
            ClusterId = 0, IsPrimary = true, Weight = 1f, Level = 0, Kind = StrokeKind.Ramp
        };
        ramp.A = mainGround.A;
        ramp.B = deckStub.A;
        store.AddStroke(ramp);

        /*
         * An orphan bundle on the deck, directly above ground the main component
         * already covers.
         */
        var orphan = _add(store, 0f, 60f, 200f, 60f, level: 1);

        _run(store);

        var connectors = _connectors(store);
        Assert.Single(connectors);

        Assert.Equal((sbyte)1, connectors[0].A.Level);
        Assert.Equal((sbyte)1, connectors[0].B.Level);
        Assert.True(connectors[0].A == deckStub.B || connectors[0].B == deckStub.B,
            "the deck orphan was not attached to the deck");
    }


    /**
     * The corridor branch, on a deck. Its intermediate junction is created by the pass
     * itself and used to be left on the default level 0, which turns each half of a
     * level-1 corridor into a street joining two decks. NetworkBuilder is what makes
     * that a refusal rather than a silently broken graph.
     */
    [Fact]
    public void ALongDeckCorridorStaysOnItsOwnDeck()
    {
        var store = new StrokeStore(ClusterSize);

        var main = _add(store, -600f, 0f, -400f, 0f, level: 1);
        var orphan = _add(store, 200f, 0f, 400f, 0f, level: 1);

        Assert.True(Vector2.Distance(main.B.Pos, orphan.A.Pos) > 300f,
            "the fixture is supposed to reach the multi-stroke corridor branch");

        _run(store);

        var connectors = _connectors(store);
        Assert.Equal(2, connectors.Count);

        foreach (var c in connectors)
        {
            Assert.Equal((sbyte)1, c.A.Level);
            Assert.Equal((sbyte)1, c.B.Level);
        }
    }


    /**
     * With no junction at all on the orphan's deck, the pass refuses rather than
     * guessing - the orphan stays visibly disconnected, which is survivable, whereas a
     * street climbing a deck over no distance is not.
     */
    [Fact]
    public void AnOrphanWithNoPartnerOnItsDeckIsLeftAlone()
    {
        var store = new StrokeStore(ClusterSize);

        _add(store, -400f, 0f, -200f, 0f);
        _add(store, 0f, 0f, 200f, 0f, level: 1);

        _run(store);

        Assert.Empty(_connectors(store));
        Assert.Equal(2, StreetHarness.CountComponents(store));
    }


    /**
     * And the shape a flat city actually has: every junction on level 0, so the filter
     * changes nothing. The eight recorded fingerprints say this for whole generated
     * cities; this says it for the branch in isolation.
     */
    [Fact]
    public void AGroundOnlyPassBridgesEverythingItAlwaysDid()
    {
        var store = new StrokeStore(ClusterSize);

        _add(store, -400f, 50f, -200f, 50f);
        _add(store, -100f, 50f, 100f, 50f);
        _add(store, -400f, 300f, -200f, 300f);

        _run(store);

        Assert.Equal(2, _connectors(store).Count);
        Assert.Equal(1, StreetHarness.CountComponents(store));
    }
}
