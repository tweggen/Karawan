using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using builtin.modules.satnav;
using builtin.modules.satnav.desc;
using engine.navigation;
using engine.world;
using Xunit;

namespace JoyceCode.Tests.builtin.modules.satnav;


/**
 * Where the satnav guideline is drawn.
 *
 * A NavJunction's Position is the ground plus ClusterNavigationHeight, which is the
 * VEHICLE hover reference and not a surface anybody stands on. The ribbon was built from
 * it and then lowered by a flat 0.5 m by its parent transform, which came out at ground
 * plus 2.5 against a road at ground plus 2.0 - so it floated half a metre, in the flat game
 * too.
 *
 * The emission itself is inside a queued main-thread action in a module that needs a booted
 * engine; RouteRibbon is where the arithmetic lives so that it is checkable at all, and a
 * source scan stands in for the call site.
 */
public class RouteRibbonTests
{
    private static NavJunction _junction(float x, float z, float ground)
        => NavJunction.At(new Vector3(x, 0f, z), ground);


    private static NavLane _lane(NavJunction a, NavJunction b) => new()
    {
        Start = a,
        End = b,
        Length = Vector3.Distance(a.Position, b.Position),
        AllowedTypes = new TransportationTypeFlags(TransportationType.Car)
    };


    /**
     * The one quad a lane with no road surface under it produces, in the corner-and-two-
     * edges form the ribbon used to be built in - so that what these assertions say about
     * the shape is unchanged by the ribbon having become a strip of them.
     */
    private static void _oneQuadFor(
        NavLane nl, TransportationType tt,
        out Vector3 v3Origin, out Vector3 v3Across, out Vector3 v3Along)
    {
        var quads = new List<RouteRibbon.Quad>();
        RouteRibbon.QuadsFor(nl, tt, quads, new List<float>());

        Assert.Single(quads);

        v3Origin = quads[0].V00;
        v3Across = quads[0].V10 - quads[0].V00;
        v3Along = quads[0].V01 - quads[0].V00;
    }


    /**
     * The road surface, written out here rather than borrowed from the ribbon, so the two
     * are compared through independent expressions. This is the same term
     * GenerateClusterStreetsOperator and JunctionCollider build the carriageway at.
     */
    private static float _roadSurfaceOf(float ground)
        => ground + MetaGen.CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE;


    /**
     * The reported defect, in arithmetic. The ribbon lies on the road, not above the
     * height a ship hovers at.
     */
    [Fact]
    public void TheRibbonLiesOnTheRoadAndNotAtTheHoverHeight()
    {
        var nj = _junction(0f, 0f, 37f);

        float y = RouteRibbon.PointOn(nj, TransportationType.Car).Y;

        Assert.Equal(_roadSurfaceOf(37f) + RouteRibbon.Lift, y, 4);

        /*
         * And it is well clear of where it was: the junction's own Y less the half metre
         * the parent transform used to take off.
         */
        Assert.Equal(0.5f, nj.Position.Y - 0.5f - _roadSurfaceOf(37f), 4);
        Assert.True(nj.Position.Y - 0.5f - y > 0.35f,
            "the ribbon has not actually come down off the hover height");
    }


    /**
     * The lift is small enough not to read as floating and big enough to be a lift.
     *
     * Bounded against the ship's own clearance over the same surface rather than against a
     * bare number, since that is the thing it must stay far below.
     */
    [Fact]
    public void TheLiftIsSmallAgainstTheShipsOwnClearance()
    {
        float shipClearance = global::engine.physics.HoverSurfaceProbe.SurfaceClearance;

        Assert.True(RouteRibbon.Lift > 0f, "a ribbon in the road z-fights with it");
        Assert.True(RouteRibbon.Lift <= shipClearance / 5f,
            $"a lift of {RouteRibbon.Lift} against a hover clearance of {shipClearance} "
            + "reads as floating rather than as lying on the road");
    }


    /**
     * A pedestrian route is drawn on the pavement, which is one kerb higher than the
     * carriageway because the block floor is extruded that far.
     *
     * Nothing shipped asks for one since the quest guidelines are all Car, but a ribbon
     * that silently used the carriageway height would be sunk into the kerb slab it is
     * drawn on, and the difference is exactly the term that decides it.
     */
    [Fact]
    public void APedestrianRibbonLiesOnThePavement()
    {
        var nj = _junction(0f, 0f, 12f);

        float onFoot = RouteRibbon.SurfaceHeightOf(nj, TransportationType.Pedestrian);
        float driving = RouteRibbon.SurfaceHeightOf(nj, TransportationType.Car);

        Assert.Equal(NavJunction.WalkingHeightOf(12f), onFoot, 4);
        Assert.Equal(MetaGen.QuarterSidewalkOffset, onFoot - driving, 4);
    }


    /**
     * The quad is centred on the lane and one Width across, which is what puts the
     * guideline down the middle of whatever lane the route chose.
     */
    [Fact]
    public void TheQuadIsCentredOnItsLane()
    {
        var njA = _junction(0f, 0f, 5f);
        var njB = _junction(100f, 0f, 5f);

        _oneQuadFor(
            _lane(njA, njB), TransportationType.Car,
            out var v3Origin, out var v3Across, out var v3Along);

        /*
         * Four metres, pinned on the number: the report was about where the ribbon is,
         * and the width is what says the guideline covers a carriageway rather than a
         * kerb line.
         */
        Assert.Equal(4f, RouteRibbon.Width);
        Assert.Equal(RouteRibbon.Width, v3Across.Length(), 4);

        /*
         * Running along +X, so the quad spans +/- half a width in Z about the lane.
         */
        Assert.Equal(0f, v3Origin.X, 4);
        Assert.Equal(RouteRibbon.Width / 2f, Single.Abs(v3Origin.Z), 4);
        Assert.Equal(-v3Origin.Z, (v3Origin + v3Across).Z, 4);

        Assert.Equal(100f, v3Along.X, 4);
        Assert.Equal(0f, v3Along.Z, 4);

        /*
         * And across the lane rather than along it.
         */
        Assert.Equal(0f, Vector3.Dot(v3Across, v3Along), 3);
    }


    /**
     * Over a road that climbs, the ribbon climbs with it - each end takes its own
     * junction's height, so the quad is a sloping strip and not a flat one at the start's
     * height with the road disappearing under it.
     */
    [Fact]
    public void TheRibbonFollowsASlopingRoad()
    {
        var njA = _junction(0f, 0f, 10f);
        var njB = _junction(100f, 0f, 16f);

        _oneQuadFor(
            _lane(njA, njB), TransportationType.Car,
            out var v3Origin, out var v3Across, out var v3Along);

        Assert.Equal(6f, v3Along.Y, 4);

        /*
         * All four corners, against the road at their own end.
         */
        float lift = RouteRibbon.Lift;
        Assert.Equal(_roadSurfaceOf(10f) + lift, v3Origin.Y, 4);
        Assert.Equal(_roadSurfaceOf(10f) + lift, (v3Origin + v3Across).Y, 4);
        Assert.Equal(_roadSurfaceOf(16f) + lift, (v3Origin + v3Along).Y, 4);
        Assert.Equal(_roadSurfaceOf(16f) + lift, (v3Origin + v3Across + v3Along).Y, 4);
    }


    /**
     * The lift is a lift and not a shift: the ribbon's plan position is the lane's.
     */
    [Fact]
    public void TheRibbonStaysWhereTheLaneIsInPlan()
    {
        var nj = _junction(-1234f, 567f, 8f);

        Vector3 p = RouteRibbon.PointOn(nj, TransportationType.Car);

        Assert.Equal(nj.Position.X, p.X, 4);
        Assert.Equal(nj.Position.Z, p.Z, 4);
    }


    /**
     * Nothing between RouteRibbon and the screen adds a height of its own.
     *
     * ToSomewhere._onJunctions runs inside a queued main-thread action in a module that
     * needs a booted engine, a physics world and the satnav module, so it is not exercised.
     * What it used to do - build the quads at the junctions' navigation height and take a
     * flat half metre off with the parent transform - is exactly what a scan can see.
     */
    [Fact]
    public void TheGuidelineIsBuiltByTheRibbonWithNoOffsetOfItsOwn()
    {
        string path = global::engine.GameRoot.PathTo("JoyceCode")
                      + "/engine/quest/ToSomewhere.cs";

        Assert.True(File.Exists(path), $"could not find ToSomewhere at {path}");

        string source = File.ReadAllText(path);

        /*
         * The whole mesh, in one expression. It used to be a loop here, and mutation
         * testing found that taking only the FIRST quad of each lane passed everything -
         * every corner right, the road cut straight across between them, and a scan seeing
         * only the name of the call. So the loop moved into RouteRibbon.MeshFor, which a
         * test can drive over a real city, and what is left here is one line to scan for.
         */
        Assert.Contains(
            "RouteRibbon.MeshFor(listLanes, TransportType);",
            source.Replace("\r\n", "\n"));

        Assert.DoesNotContain("AddQuadCornersUV", source);
        Assert.DoesNotContain("AddQuadXYUV", source);
        Assert.DoesNotContain("-0.5f*Vector3.UnitY", source);
        Assert.DoesNotContain("nl.Start.Position+2f*vu3Right", source);
    }
}
