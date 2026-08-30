using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using builtin.modules.satnav;
using builtin.modules.satnav.desc;
using engine.navigation;
using Xunit;

namespace JoyceCode.Tests.builtin.modules.satnav;


/**
 * Which lanes the player's satnav routes over.
 *
 * The nav map holds two networks in the same space: car lanes down the middle of each
 * carriageway, pedestrian lanes along the pavement round every block. Both the cursor that
 * finds a starting lane near a point and the A* that walks the graph filter on a
 * TransportationType, and if that type is wrong the route is planned - and the guideline
 * ribbon drawn - on the wrong surface entirely.
 *
 * LocalPathfinder and the cursors need nothing from the container, so all of this is real
 * routing rather than a scan. What is NOT covered is Route.Search, which hops onto the
 * engine's logical thread, and the quest call sites, which live in nogameCode; a source
 * scan stands in for those.
 */
public class SatnavRouteTransportTests
{
    private const float CarLaneZ = 0f;
    private const float PavementZ = -8f;


    private static NavJunction _junction(float x, float z)
        => NavJunction.At(new Vector3(x, 0f, z), 0f);


    /**
     * A point over the network, at the height a junction stands at.
     */
    private static Vector3 _over(float x, float z)
        => new(x, NavJunction.NavigationHeightOf(0f), z);


    private static NavLane _lane(NavJunction a, NavJunction b, TransportationType types)
    {
        var lane = new NavLane
        {
            Start = a,
            End = b,
            Length = Vector3.Distance(a.Position, b.Position),
            AllowedTypes = new TransportationTypeFlags(types)
        };

        a.StartingLanes.Add(lane);
        b.EndingLanes.Add(lane);

        return lane;
    }


    /**
     * A street with both networks on it: a car lane down the middle and a pavement lane
     * eight metres to one side, running between the same two junctions in plan.
     *
     * That is the shape the generator really produces - GenerateNavMapOperator emits car
     * lanes from the strokes and pedestrian lanes from the quarter boundaries beside them -
     * and it is the shape in which asking for the wrong type still finds *a* route.
     */
    private static (NavCluster Cluster, List<NavLane> Car, List<NavLane> Foot) _street()
    {
        var carJunctions = new List<NavJunction>();
        var footJunctions = new List<NavJunction>();
        for (int i = 0; i <= 3; ++i)
        {
            carJunctions.Add(_junction(i * 100f, CarLaneZ));
            footJunctions.Add(_junction(i * 100f, PavementZ));
        }

        var nc = new NavCluster { Id = "satnav-transport" };
        var content = new NavClusterContent { Cluster = nc };

        var car = new List<NavLane>();
        var foot = new List<NavLane>();
        for (int i = 0; i < 3; ++i)
        {
            car.Add(_lane(carJunctions[i], carJunctions[i + 1], TransportationType.Car));
            foot.Add(_lane(
                footJunctions[i], footJunctions[i + 1], TransportationType.Pedestrian));
        }

        content.Lanes.AddRange(car);
        content.Lanes.AddRange(foot);
        content.Junctions.AddRange(carJunctions);
        content.Junctions.AddRange(footJunctions);

        /*
         * Recompile builds the octrees but does not set the cluster's own AABB, which is
         * what NavCluster.TryCreateCursor rejects a position against first.
         */
        nc.AABB = new global::engine.geom.AABB(
            new Vector3(-50f, -50f, -50f), new Vector3(350f, 50f, 50f));

        nc.Content = content;
        content.Recompile();

        return (nc, car, foot);
    }


    /**
     * Through RoutePlan, which is what Route.Search calls - so a transport type hardcoded
     * on the way to the cursors is seen here rather than only at the call site.
     */
    private static Task<List<NavLane>> _route(
        NavCluster nc, Vector3 from, Vector3 to, TransportationType type)
        => RoutePlan.PlanAsync(nc, from, to, type);


    /**
     * Which side of the street every junction of a route stands on.
     *
     * Asserted on the geometry rather than on lane identity, because the last lane of a
     * route is truncated at the target and replaced - so a route is not made only of lanes
     * the fixture handed out.
     */
    private static void _assertRunsAlong(List<NavLane> lanes, float z)
    {
        Assert.NotNull(lanes);
        Assert.NotEmpty(lanes);

        foreach (var l in lanes)
        {
            Assert.Equal(z, l.Start.Position.Z, 3);
            Assert.Equal(z, l.End.Position.Z, 3);
        }
    }


    /**
     * The reported defect. Asked to route a driver, the satnav used to plan over the
     * pavement, because Route named no transport type and LocalPathfinder's default was
     * Pedestrian - so the guideline was drawn along the kerb rather than down the road.
     *
     * The start and the end are given as the ship's own position, over the carriageway, to
     * make the point that it is not the geometry that chose the sidewalk.
     */
    [Fact]
    public async Task ADriverIsRoutedOverCarLanes()
    {
        var (nc, _, foot) = _street();

        var lanes = await _route(
            nc,
            _over(10f, CarLaneZ),
            _over(290f, CarLaneZ),
            TransportationType.Car);

        _assertRunsAlong(lanes, CarLaneZ);
        Assert.All(lanes, l => Assert.DoesNotContain(l, foot));
    }


    /**
     * And the other way round, so that the type is doing the work rather than the car
     * network happening to be nearer everything.
     */
    [Fact]
    public async Task AWalkerIsRoutedOverPedestrianLanes()
    {
        var (nc, car, _) = _street();

        var lanes = await _route(
            nc,
            _over(10f, CarLaneZ),
            _over(290f, CarLaneZ),
            TransportationType.Pedestrian);

        _assertRunsAlong(lanes, PavementZ);
        Assert.All(lanes, l => Assert.DoesNotContain(l, car));
    }


    /**
     * The two halves have to be given the SAME type, and this is what happens when they
     * are not: a cursor refuses to hand back a lane of the wrong kind, so a route planned
     * from a pedestrian cursor by a car A* does not merely take a poor path, it never
     * leaves the start.
     */
    [Fact]
    public async Task MixingTheTwoFindsNoRouteAtAll()
    {
        var (nc, _, _) = _street();

        var cursors = await Task.WhenAll(
            nc.TryCreateCursor(
                _over(10f, PavementZ), TransportationType.Pedestrian),
            nc.TryCreateCursor(
                _over(290f, PavementZ), TransportationType.Pedestrian));

        Assert.False(cursors[0].IsNil());

        Assert.Null(new LocalPathfinder(
            cursors[0], cursors[1], TransportationType.Car).Pathfind());
    }


    /**
     * A cursor near the carriageway still returns a pedestrian lane when a pedestrian asks,
     * rather than the nearer car one - which is the property that makes the pathfinder's
     * own filter necessary rather than incidental.
     */
    [Fact]
    public async Task ACursorAnswersWithTheTypeItWasAskedFor()
    {
        var (nc, car, foot) = _street();
        Vector3 overTheRoad = _over(150f, CarLaneZ + 0.5f);

        var asDriver = await nc.TryCreateCursor(overTheRoad, TransportationType.Car);
        var asWalker = await nc.TryCreateCursor(overTheRoad, TransportationType.Pedestrian);

        Assert.Contains(asDriver.Lane, car);
        Assert.Contains(asWalker.Lane, foot);
    }


    /**
     * Every shipped quest guideline says which network it is for.
     *
     * The four sites are in nogameCode, which the test assembly cannot reference, and all
     * four sense the player through the hover ship's own physics name - so all four are
     * for a driver. ToSomewhere.TransportType is `required`, so a new one cannot compile
     * without saying; this is here for the value rather than the presence.
     */
    [Fact]
    public void EveryShippedQuestGuidelineRoutesOnCarLanes()
    {
        string root = global::engine.GameRoot.PathTo("nogameCode");
        Assert.False(String.IsNullOrEmpty(root), "could not locate the checkout");

        var sites = Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Select(f => (Path: f, Text: File.ReadAllText(f)))
            .Where(f => f.Text.Contains("new engine.quest.ToLocation()")
                        || f.Text.Contains("new engine.quest.TrailVehicle()"))
            .ToList();

        Assert.True(sites.Count >= 4,
            $"only found {sites.Count} quest navigation targets - this scan has stopped "
            + "finding them");

        var offenders = sites
            .Where(f => !f.Text.Contains(
                "TransportType = engine.navigation.TransportationType.Car"))
            .Select(f => Path.GetRelativePath(root, f.Path).Replace('\\', '/'))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        Assert.True(0 == offenders.Count,
            "these create a quest navigation target that is not routed on car lanes, and "
            + "the player drives to all of them:\n  " + String.Join("\n  ", offenders));
    }

    /**
     * The route stops where it passes the target rather than at the junction beyond it,
     * and the junction it synthesises to do that goes through the NavJunction factory -
     * so it carries a ground height consistent with its position rather than a zero.
     */
    [Fact]
    public async Task TheRouteIsCutWhereItPassesTheTarget()
    {
        var (nc, _, _) = _street();

        var lanes = await _route(
            nc, _over(10f, CarLaneZ), _over(150f, CarLaneZ), TransportationType.Car);

        Assert.NotNull(lanes);
        Assert.NotEmpty(lanes);

        var end = lanes[^1].End;
        Assert.Equal(150f, end.Position.X, 2);
        Assert.Equal(NavJunction.NavigationHeightOf(end.GroundHeight), end.Position.Y, 4);
    }

    /**
     * Route hands its own transport type to the planner.
     *
     * Route.Search hops onto the engine's logical thread and cannot be exercised, so the
     * one line in it that still decides anything is scanned for. That is a proxy and it is
     * a weak one; it exists because this exact line - a transport type not passed through
     * on the way to the cursors - is the defect being fixed, and it survived every
     * behavioural test in this file until the planning moved out of Route.
     */
    [Fact]
    public void RouteAsksThePlannerForItsOwnTransportType()
    {
        string path = global::engine.GameRoot.PathTo("JoyceCode")
                      + "/builtin/modules/satnav/Route.cs";

        Assert.True(File.Exists(path), $"could not find Route at {path}");

        string source = File.ReadAllText(path);

        Assert.Contains("RoutePlan.PlanAsync(", source);
        Assert.Contains("_b.GetLocation(), TransportType)", source);

        /*
         * And it plans nothing itself, so there is nowhere else for a type to be decided.
         */
        Assert.DoesNotContain("new LocalPathfinder", source);
        Assert.DoesNotContain("TryCreateCursor", source);
    }
}
