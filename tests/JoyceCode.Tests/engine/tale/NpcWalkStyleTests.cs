using System;
using System.IO;
using System.Linq;
using System.Numerics;
using builtin.modules.satnav;
using builtin.modules.satnav.desc;
using engine.tale;
using Xunit;

namespace JoyceCode.Tests.engine.tale;


/**
 * KAR-481: walking TALE NPCs no longer share one spot.
 *
 * Measured before (Yelukhdidru, 279 NPCs): every NPC walked at exactly 4.5 km/h, 1.5 m off
 * the kerb, on a deterministic route; the warm-up gave every NPC in transit one
 * TransitStart, and 82-97 of about 100-140 of them shared origin and destination as well -
 * so they materialised and walked as one figure. Each NPC now has its own NpcWalkStyle.
 */
public class NpcWalkStyleTests
{
    private static string Root => Path.GetFullPath(Path.Combine(global::engine.GameRoot.PathTo("JoyceCode"), ".."));


    [Fact]
    public void TheSameNpcAlwaysWalksTheSameWay()
    {
        Assert.Equal(NpcWalkStyle.For(4711), NpcWalkStyle.For(4711));
    }


    [Fact]
    public void EveryValueStaysInItsRange()
    {
        for (int id = 0; id < 5000; ++id)
        {
            var s = NpcWalkStyle.For(id);
            Assert.InRange(s.SidewalkOffset, NpcWalkStyle.MinSidewalkOffset, NpcWalkStyle.MaxSidewalkOffset);
            Assert.InRange(s.SpeedFactor, 1f - NpcWalkStyle.SpeedSpread, 1f + NpcWalkStyle.SpeedSpread);
            Assert.InRange(s.DepartureJitterSeconds, -NpcWalkStyle.DepartureSpreadSeconds, NpcWalkStyle.DepartureSpreadSeconds);
            Assert.InRange(s.RoutePhaseMetres, -NpcWalkStyle.RoutePhaseSpreadMetres, NpcWalkStyle.RoutePhaseSpreadMetres);
        }
    }


    /**
     * A cluster's NPCs - the largest group that shared one route in Yelukhdidru was under a
     * hundred - spread over the ranges rather than collapsing onto a few values.
     */
    [Fact]
    public void ACitysNpcsDoNotShareAWalk()
    {
        var styles = Enumerable.Range(0, 279).Select(NpcWalkStyle.For).ToList();

        Assert.Equal(279, styles.Distinct().Count());
        Assert.True(styles.Select(s => MathF.Round(s.RoutePhaseMetres)).Distinct().Count() >= 25,
            "route phases collapse onto too few metres");
        Assert.True(styles.Max(s => s.SidewalkOffset) - styles.Min(s => s.SidewalkOffset) > 0.8f);
        Assert.True(styles.Max(s => s.SpeedFactor) - styles.Min(s => s.SpeedFactor) > 0.25f);

        /*
         * Fifty of them on one 200 m route at one t - about the largest group measured
         * sharing a route - stand apart in the plane of the pavement: along the route by
         * their phase, across it by their offset. Pairs closer than a body (0.4 m) are rare;
         * uniform spread over 30 m x 1 m predicts about 1.7 %. Their speeds part them further
         * once they walk.
         */
        var group = styles.Take(50)
            .Select(s => new Vector2(s.RouteFraction(0.5f, 200f) * 200f, s.SidewalkOffset)).ToList();
        int pairs = 0, touching = 0;
        for (int i = 0; i < group.Count; ++i)
        for (int j = i + 1; j < group.Count; ++j)
        {
            ++pairs;
            if (Vector2.Distance(group[i], group[j]) < 0.4f) ++touching;
        }
        Assert.True(touching < pairs * 0.05f, $"{touching} of {pairs} pairs closer than 0.4 m");
    }


    [Fact]
    public void TheDepartureJitterSurvivesTheClamp()
    {
        // Every storylet over two game hours clamps to 300 s; jitter must still tell them apart.
        var stays = Enumerable.Range(0, 100).Select(id => NpcWalkStyle.For(id).StayDurationSeconds(300f)).ToList();
        Assert.True(stays.Distinct().Count() > 90);
        Assert.All(stays, s => Assert.InRange(s, 300f - NpcWalkStyle.DepartureSpreadSeconds, 300f + NpcWalkStyle.DepartureSpreadSeconds));

        // ...and never makes a short one negative.
        Assert.All(Enumerable.Range(0, 100), id => Assert.True(NpcWalkStyle.For(id).StayDurationSeconds(2f) >= 2f));
    }


    [Fact]
    public void ThePhaseStaysOnTheRoute()
    {
        var s = new NpcWalkStyle(1.5f, 1f, 0f, 15f);
        Assert.Equal(1f, s.RouteFraction(0.95f, 100f));
        Assert.Equal(0.25f, s.RouteFraction(0.1f, 100f), 5);
        Assert.Equal(0.3f, s.RouteFraction(0.3f, 0f));
        Assert.Equal(0f, new NpcWalkStyle(1.5f, 1f, 0f, -15f).RouteFraction(0.05f, 100f));
    }


    [Fact]
    public void AWalkersOffsetMovesItsWaypointOntoThePavement_AndTheDefaultIsUnchanged()
    {
        var a = NavJunction.At(new Vector3(0f, 0f, 0f), 0f);
        var b = NavJunction.At(new Vector3(20f, 0f, 0f), 0f);
        var lane = new NavLane { Start = a, End = b, KerbSide = new Vector3(0f, 0f, 1f) };

        Assert.Equal(PedestrianRoute.WaypointAt(lane, PedestrianRoute.SidewalkOffset), PedestrianRoute.WaypointFor(lane));
        Assert.Equal(0.7f, PedestrianRoute.WaypointAt(lane, 2.2f).Z - PedestrianRoute.WaypointAt(lane, 1.5f).Z, 4);

        // A crossing has no kerb side and keeps the centre line whatever the walker's offset.
        var crossing = new NavLane { Start = a, End = b };
        Assert.Equal(PedestrianRoute.WaypointFor(crossing), PedestrianRoute.WaypointAt(crossing, 2f));
        Assert.Equal(PedestrianRoute.SidewalkOffset, new RoutingPreferences().SidewalkOffset);
    }


    /** The call sites run only in the game; that each asks the NPC's style is scanned. */
    [Fact]
    public void TheWalkerTakesItsStyleEverywhereItMoves()
    {
        string strategy = File.ReadAllText(Path.Combine(Root, "nogameCode/nogame/characters/citizen/TaleEntityStrategy.cs"));
        string spawn = File.ReadAllText(Path.Combine(Root, "nogameCode/nogame/characters/citizen/TaleSpawnOperator.cs"));
        string builder = File.ReadAllText(Path.Combine(Root, "nogameCode/nogame/characters/citizen/StreetRouteBuilder.cs"));

        Assert.Contains("BasicSpeed = 4.5f / 3.6f * NpcWalkStyle.For(schedule.NpcId).SpeedFactor", strategy);
        Assert.Contains("_stayAt.StayDurationSeconds = NpcWalkStyle.For(_npcId).StayDurationSeconds(realSeconds);", strategy);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(strategy,
            @"_routingPreferences\.SidewalkOffset = NpcWalkStyle\.For\(").Count);
        Assert.Contains("t = NpcWalkStyle.For(schedule.NpcId).RouteFraction(t, routeLength);", spawn);
        Assert.Contains("SidewalkOffset = walkStyle.SidewalkOffset", spawn);
        Assert.Contains("preferences?.SidewalkOffset ?? builtin.modules.satnav.PedestrianRoute.SidewalkOffset", builder);
    }
}
