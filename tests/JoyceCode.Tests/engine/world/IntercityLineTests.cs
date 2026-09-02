using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using builtin.tools;
using engine.world;
using Xunit;

namespace JoyceCode.Tests.engine.world;


/**
 * The intercity tram rides its own track.
 *
 * It did not. nogame.characters.intercity.GenerateCharacterOperator built its two route
 * ends at ClusterA.AverageHeight + 20 and ClusterB.AverageHeight + 20 and flew the
 * straight chord between them, while the track those two cities are joined by -
 * nogame.intercity.IntercityTrackElevationOperator, burning a flat ribbon into the
 * terrain - sits at min(AverageHeight(A), AverageHeight(B)). Over the shipped world's
 * 114 lines that difference is 23.5 m at the median and 89.3 m at the worst, so the
 * vehicle ran a median 43.5 m over its own track at the higher end.
 *
 * The two heights are decided in one place now (engine.world.IntercityLine) and the
 * vehicle's comes from the track's. There is no sampling, because a track that is one
 * height everywhere has nothing to sample.
 */
public class IntercityLineTests
{
    /**
     * A spread of city pairs, including equal heights and both orders of a big drop.
     */
    public static IEnumerable<object[]> Pairs()
    {
        foreach (var (a, b) in new[]
                 {
                     (0f, 0f), (37.76f, 37.76f), (-9.5f, -9.5f),
                     (10f, 40f), (40f, 10f), (0f, 89.34f), (89.34f, 0f),
                     (-8.2f, 71.9f), (71.9f, -8.2f), (12.25f, 12.5f)
                 })
        {
            yield return new object[] { a, b };
        }
    }


    /**
     * The track is the LOWER of the two cities, which is what the elevation operator
     * writes. Stated here because the vehicle's height is derived from it, so if this
     * moves the vehicle has to move with it rather than being corrected separately -
     * which is the whole shape of the defect.
     */
    [Theory]
    [MemberData(nameof(Pairs))]
    public void TheTrackIsTheLowerOfTheTwoCities(float a, float b)
    {
        Assert.Equal(Single.Min(a, b), IntercityLine.TrackHeightOf(a, b));
        Assert.Equal(IntercityLine.TrackHeightOf(a, b), IntercityLine.TrackHeightOf(b, a));
    }


    /**
     * Two cities of equal average height are EXACTLY where they were.
     *
     * This is the clearance's derivation, not a coincidence: the shipped expression put
     * the vehicle at each city's own average + 20 and the track at the minimum of the
     * two, so for a matched pair the vehicle was 20 m over its track and stays there,
     * float for float. ClusterDesc.AverageHeight is computed from the unflattened ground
     * whether or not the cluster is ironed flat, so this holds in the flat game and the
     * terrain following one alike.
     */
    [Fact]
    public void AMatchedPairOfCitiesDoesNotMove()
    {
        foreach (float h in new[] { 0f, 37.76f, -9.5f, 71.9f, 0.1f, 1e6f })
        {
            Assert.Equal(h + 20f, IntercityLine.VehicleHeightOf(IntercityLine.TrackHeightOf(h, h)));
        }
    }


    /**
     * The vehicle takes the LOWER city's shipped height and never the higher one's.
     *
     * Asserted as an equality against the shipped expression at the lower end rather than
     * as an inequality, because "lower than it was" is also satisfied by any number at
     * all below the old one.
     */
    [Theory]
    [MemberData(nameof(Pairs))]
    public void TheVehicleKeepsTheLowerEndAndDropsToIt(float a, float b)
    {
        float now = IntercityLine.VehicleHeightOf(IntercityLine.TrackHeightOf(a, b));

        Assert.Equal(Single.Min(a + 20f, b + 20f), now);
        Assert.True(now <= a + 20f);
        Assert.True(now <= b + 20f);
    }


    /**
     * The whole route is level, and it is exactly the clearance above the track.
     *
     * Driven through the REAL builtin.tools.SegmentNavigator over the route
     * IntercityLine.RouteBetween builds - the same object
     * SimpleNavigationBehavior.Behave reads its position out of - rather than asserted
     * about the two endpoints, because the defect was never at an endpoint: the two ends
     * were each individually "correct" for their own city and it was the chord between
     * them that flew.
     */
    [Theory]
    [MemberData(nameof(Pairs))]
    public void TheVehicleStaysTheClearanceAboveItsTrackAllTheWayAlong(float a, float b)
    {
        var v3A = new Vector3(-4000f, 17f, 500f);
        var v3B = new Vector3(3000f, -31f, -2500f);
        float track = IntercityLine.TrackHeightOf(a, b);

        var route = IntercityLine.RouteBetween(v3A, v3B, track);
        var nav = new SegmentNavigator() { SegmentRoute = route, Speed = 60f };
        nav.NavigatorLoad();

        /*
         * Well over one full loop of a 7.6 km line at 60 m/s.
         */
        for (int i = 0; i < 600; ++i)
        {
            nav.NavigatorBehave(1f);
            nav.NavigatorGetTransformation(out var v3Pos, out _);

            Assert.Equal(IntercityLine.VehicleHeightOf(track), v3Pos.Y);
            Assert.Equal(IntercityLine.VehicleClearance, v3Pos.Y - track);
        }
    }


    /**
     * The route ignores the stations' own Y.
     *
     * A station is a point on its cluster's boundary rectangle and carries the cluster's
     * nominal Pos.Y, which GenerateClustersOperator draws at random between 10 and 40 for
     * every city but the start one. Nothing about the line should depend on it.
     */
    [Fact]
    public void TheStationsOwnHeightDoesNotReachTheRoute()
    {
        var route = IntercityLine.RouteBetween(
            new Vector3(100f, 38.69f, 0f), new Vector3(-100f, 11.13f, 40f), 12f);
        var other = IntercityLine.RouteBetween(
            new Vector3(100f, -1000f, 0f), new Vector3(-100f, 4000f, 40f), 12f);

        Assert.Equal(route.Segments[0].Position, other.Segments[0].Position);
        Assert.Equal(route.Segments[1].Position, other.Segments[1].Position);
        Assert.Equal(IntercityLine.VehicleHeightOf(12f), route.Segments[0].Position.Y);
        Assert.Equal(IntercityLine.VehicleHeightOf(12f), route.Segments[1].Position.Y);
    }


    /**
     * Over the world the game actually builds.
     *
     * Loose bounds on purpose - this is not a baseline, and pinning the exact medians
     * would make an ordinary change to city layout look like a regression. What it does
     * guard is that the world still HAS the property the fix is about: connected cities
     * at markedly different heights. Measured 2026-08-31 on this runtime: 70 cities,
     * 114 lines, |dAverage| median 23.5 m / p95 66.8 m / max 89.3 m; 114 of the 228 route
     * ends move, all of them downward, by a median 23.5 m.
     */
    [Fact]
    public void OverTheShippedWorldOnlyTheHigherEndOfEachLineComesDown()
    {
        var lines = IntercityWorldHarness.Lines();
        Assert.True(lines.Count > 50, $"only {lines.Count} intercity lines");

        int nMoved = 0, nRaised = 0;
        var drops = new List<double>();
        foreach (var l in lines)
        {
            float now = IntercityLine.VehicleHeightOf(l.TrackHeight);

            Assert.True(now <= l.ShippedEndA + 1e-4f);
            Assert.True(now <= l.ShippedEndB + 1e-4f);
            Assert.Equal(Single.Min(l.ShippedEndA, l.ShippedEndB), now);

            foreach (float end in new[] { l.ShippedEndA, l.ShippedEndB })
            {
                if (end - now > 1e-4f) { ++nMoved; drops.Add(end - now); }
                if (now - end > 1e-4f) ++nRaised;
            }
        }

        Assert.Equal(0, nRaised);
        Assert.True(nMoved > lines.Count / 2,
            $"only {nMoved} of {2 * lines.Count} route ends move; the world has no height spread");

        drops.Sort();
        Assert.True(drops[drops.Count / 2] > 5.0,
            $"median drop is only {drops[drops.Count / 2]:F2} m");
    }


    /**
     * Neither height is written anywhere else.
     *
     * The subsystem lives entirely in nogameCode, which this assembly does not reference,
     * so the arithmetic was hoisted into Joyce where the tests above can drive it and a
     * scan is left with only the one line that reaches for it. Absence as well as
     * presence: re-adding "AverageHeight + 20f" beside a call to VehicleHeightOf compiles,
     * runs, and passes every test above.
     */
    [Fact]
    public void TheIntercitySourcesAskForTheLineHeightAndNothingElse()
    {
        string root = global::engine.GameRoot.PathTo("JoyceCode");
        string nogame = Path.GetFullPath(Path.Combine(root, "..", "nogameCode", "nogame"));
        Assert.True(Directory.Exists(nogame), $"could not find nogameCode at {nogame}");

        string vehicle = File.ReadAllText(Path.Combine(
            nogame, "characters", "intercity", "GenerateCharacterOperator.cs"));
        string network = File.ReadAllText(Path.Combine(nogame, "intercity", "Network.cs"));
        string track = File.ReadAllText(Path.Combine(
            nogame, "intercity", "IntercityTrackElevationOperator.cs"));

        /*
         * The whole expression, not just the call: passing anything other than the line's
         * own track height into RouteBetween - a constant, one city's average, zero -
         * compiles and passes every test above.
         */
        Assert.Contains(
            "_createIntercity(line.StationA.Position, line.StationB.Position, line.Height)",
            vehicle);
        Assert.Contains("IntercityLine.RouteBetween(caPos, cbPos, trackHeight)", vehicle);
        Assert.DoesNotContain("AverageHeight", vehicle);
        Assert.DoesNotContain("SegmentRoute sr = new", vehicle);

        /*
         * The whole assignment, whitespace insensitive. A call to TrackHeightOf whose
         * result is then scaled, offset or discarded is what "contains the name" buys,
         * and it survived a scan that only looked for the name.
         */
        Assert.Matches(
            new Regex(@"Height\s*=\s*engine\.world\.IntercityLine\.TrackHeightOf\(\s*"
                      + @"clusterA\.AverageHeight,\s*clusterB\.AverageHeight\)\s*$",
                RegexOptions.Multiline),
            network);
        Assert.DoesNotContain("Single.Min(clusterA.AverageHeight", network);

        /*
         * The track operator writes _line.Height and nothing derived from a city.
         */
        Assert.Contains("epxDest.Height = _line.Height;", track);
        Assert.DoesNotContain("AverageHeight", track);
    }
}
