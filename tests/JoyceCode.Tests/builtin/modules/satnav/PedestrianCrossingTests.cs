using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using builtin.modules.satnav;
using builtin.modules.satnav.desc;
using engine.streets;
using engine.world;
using JoyceCode.Tests.engine.streets;
using Xunit;

namespace JoyceCode.Tests.builtin.modules.satnav;


/**
 * Which junction a pavement corner belongs to, for the purpose of crossing the road.
 *
 * A pedestrian crossing spans the carriageway AT a junction, between two of that
 * junction's own section points. GenerateNavMapOperator therefore has to group the
 * corners of every block by the junction each corner stands on, which is the delimiter's
 * own StreetPoint - see QuarterDelim. It filed them under the junction at the far end of
 * the block's next edge instead, 70 to 97 m away at the median of these cities.
 *
 * The operator itself needs a stroke store, a quarter store and the container and is not
 * exercised. What is exercised is the filing it is built from, against real generated
 * cities.
 */
public class PedestrianCrossingTests
{
    private static (ClusterDesc, StrokeStore, QuarterStore) _city(
        string idString, float size, Func<float, float, float> fHeight)
    {
        var cd = StreetHarness.MakeCluster(idString, size);
        cd.AverageHeight = 20f;
        cd.StreetHeightSource = null == fHeight
            ? new FlatStreetHeight(cd)
            : new FuncStreetHeight(fHeight);

        var store = StreetHarness.Generate(idString, size);

        return (cd, store, StreetHarness.GenerateQuarters(cd, store, idString));
    }


    /**
     * Run the filing over a whole generated city, exactly as the operator does.
     */
    private static (SortedDictionary<int, List<NavJunction>>, SortedDictionary<int, StreetPoint>)
        _fileCity(ClusterDesc cd, QuarterStore quarters)
    {
        Dictionary<(int, int), NavJunction> sidewalkJunctions = new();
        SortedDictionary<int, List<NavJunction>> byJunction = new();
        SortedDictionary<int, StreetPoint> junctionById = new();

        foreach (var quarter in quarters.GetQuarters())
        {
            if (quarter.IsInvalid()) continue;
            var delims = quarter.GetDelims();
            if (delims.Count < 3) continue;

            foreach (var delim in delims)
            {
                var key = ((int)(delim.StartPoint.X * 10), (int)(delim.StartPoint.Y * 10));
                if (!sidewalkJunctions.TryGetValue(key, out var nj))
                {
                    nj = GenerateNavMapOperator.SidewalkJunctionFor(
                        delim, cd.Pos, cd.StreetHeightSource);
                    sidewalkJunctions[key] = nj;
                }

                GenerateNavMapOperator.FileCornerUnderItsJunction(
                    delim, nj, byJunction, junctionById);
            }
        }

        return (byJunction, junctionById);
    }


    /**
     * Every corner filed under a junction is a section point of that junction.
     *
     * This is the whole claim. A corner filed under a neighbouring junction is a section
     * point of something 70 to 97 m away at the median of these cities, so the list a
     * junction is asked for when its crossings are drawn is a list of corners it does not
     * touch.
     */
    [Theory]
    [InlineData("seed000", 500f)]
    [InlineData("Yelukhdidru", 800f)]
    [InlineData("seed000", 1500f)]
    public void ACornerIsFiledUnderTheJunctionItStandsOn(string idString, float size)
    {
        var (cd, _, quarters) = _city(idString, size, (x, z) => 20f + 0.01f * x);
        cd.Pos = new Vector3(3000f, 777f, -1200f);

        var (byJunction, junctionById) = _fileCity(cd, quarters);

        int nFiled = 0;
        float worst = 0f;

        foreach (var (spId, list) in byJunction)
        {
            var sp = junctionById[spId];
            var sections = sp.GetSectionArray();

            foreach (var nj in list)
            {
                /*
                 * Back out of world space into the cluster plan the section points live
                 * in - the operator carries the cluster origin into the junction.
                 */
                var v2Corner = new Vector2(
                    nj.Position.X - cd.Pos.X, nj.Position.Z - cd.Pos.Z);

                float nearest = sections.Count == 0
                    ? Single.MaxValue
                    : sections.Min(s => (s - v2Corner).Length());

                Assert.True(nearest < 0.05f,
                    $"corner {v2Corner} is filed under junction {spId} at {sp.Pos} but is "
                    + $"{nearest:F1} m from that junction's nearest section point");

                worst = Single.Max(worst, (v2Corner - sp.Pos).Length());
                ++nFiled;
            }
        }

        Assert.True(nFiled > 0);

        /*
         * And the numbers are the ones the fix rests on: a corner stands about half a
         * carriageway from its junction. If this ever passed with a corner a street away
         * the assertion above would be measuring nothing.
         */
        Assert.True(worst < 50f,
            $"furthest filed corner is {worst:F1} m from its junction, which is a street "
            + "and not a carriageway");
    }


    /**
     * The two crossing corners of every arm of every junction are in that junction's own
     * filed list.
     *
     * This is the operator's actual use of the list. It looks the flanking section points
     * up by position in the city-wide corner table, so the crossing lanes themselves came
     * out right whichever way the corners were filed - which is exactly why the wrong
     * filing survived: the list is consulted for the dead-end case and is otherwise a
     * claim nothing checked. Here it is checked.
     */
    [Theory]
    [InlineData("seed000", 500f)]
    [InlineData("Yelukhdidru", 800f)]
    [InlineData("seed000", 1500f)]
    public void EveryCrossingCornerIsInItsJunctionsOwnList(string idString, float size)
    {
        var (cd, _, quarters) = _city(idString, size, (x, z) => 20f + 0.01f * x);

        var (byJunction, junctionById) = _fileCity(cd, quarters);

        var filedPositions = new Dictionary<int, HashSet<(int, int)>>();
        foreach (var (spId, list) in byJunction)
        {
            filedPositions[spId] = list
                .Select(nj => ((int)((nj.Position.X - cd.Pos.X) * 10),
                               (int)((nj.Position.Z - cd.Pos.Z) * 10)))
                .ToHashSet();
        }

        /*
         * All corners of the city, which is what the operator looks a crossing end up in.
         */
        var allCorners = new HashSet<(int, int)>();
        foreach (var quarter in quarters.GetQuarters())
        {
            if (quarter.IsInvalid()) continue;
            if (quarter.GetDelims().Count < 3) continue;
            foreach (var d in quarter.GetDelims())
            {
                allCorners.Add(((int)(d.StartPoint.X * 10), (int)(d.StartPoint.Y * 10)));
            }
        }

        int nCrossings = 0;

        foreach (var (spId, sp) in junctionById)
        {
            var arms = sp.GetAngleArray();
            int n = arms.Count;
            if (n < 3) continue;

            for (int i = 0; i < n; ++i)
            {
                var curr = arms[i];
                var prev = arms[(i - 1 + n) % n];
                var next = arms[(i + 1) % n];

                var ptA = sp.GetSectionPointByStroke(curr, prev);
                var ptB = sp.GetSectionPointByStroke(next, curr);
                if (ptA == null || ptB == null) continue;

                var keyA = ((int)(ptA.Value.X * 10), (int)(ptA.Value.Y * 10));
                var keyB = ((int)(ptB.Value.X * 10), (int)(ptB.Value.Y * 10));

                if (!allCorners.Contains(keyA) || !allCorners.Contains(keyB)) continue;
                if (keyA == keyB) continue;

                Assert.Contains(keyA, filedPositions[spId]);
                Assert.Contains(keyB, filedPositions[spId]);
                ++nCrossings;
            }
        }

        Assert.True(nCrossings > 0);
    }


    /**
     * The filing covers exactly the junctions the block's own corners stand on - it
     * neither drops one nor invents one.
     */
    [Theory]
    [InlineData("seed000", 500f)]
    [InlineData("Yelukhdidru", 800f)]
    [InlineData("seed000", 1500f)]
    public void TheFilingCoversExactlyTheBlocksOwnJunctions(string idString, float size)
    {
        var (cd, _, quarters) = _city(idString, size, null);

        var (byJunction, _) = _fileCity(cd, quarters);

        var byCorner = new HashSet<int>();

        foreach (var quarter in quarters.GetQuarters())
        {
            if (quarter.IsInvalid()) continue;
            var delims = quarter.GetDelims();
            if (delims.Count < 3) continue;

            foreach (var d in delims) byCorner.Add(d.StreetPoint.Id);
        }

        Assert.True(byJunction.Count > 0);
        Assert.Equal(byCorner.OrderBy(i => i), byJunction.Keys.OrderBy(i => i));
    }


    /**
     * The crossings come out in junction-id order, not in the order the blocks were traced.
     *
     * The filing decides the order the crossing loop runs in, and therefore the order the
     * crossing lanes land in every junction's lane list - which is what an A* breaks ties
     * on. With a plain Dictionary that order is the insertion order, so it depends on
     * which block the quarter store traced first AND on which half of a delimiter the
     * corner was filed under: correcting the filing would silently reorder the pedestrian
     * network. Sorted, it depends on neither, exactly as the car-lane junctions above it
     * already are.
     *
     * A source scan because the dictionary is a local of a method that needs a stroke
     * store, a quarter store and the container.
     */
    [Fact]
    public void TheCrossingFilingIsOrderedByJunctionId()
    {
        string root = global::engine.GameRoot.PathTo("JoyceCode");
        Assert.False(String.IsNullOrEmpty(root), "could not locate the checkout");

        string src = System.IO.File.ReadAllText(System.IO.Path.Combine(
            root, "builtin", "modules", "satnav", "GenerateNavMapOperator.cs"));

        Assert.Matches(
            @"SortedDictionary<int,\s*List<NavJunction>>\s+junctionsByStreetPoint",
            src);
        Assert.Matches(
            @"SortedDictionary<int,\s*StreetPoint>\s+streetPointById",
            src);
    }


}
