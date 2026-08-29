using System;
using System.IO;
using engine.physics;
using engine.world;
using Xunit;

namespace JoyceCode.Tests.engine.physics;


/**
 * The hover loop asks the physics world what it is over, instead of asking the terrain.
 *
 * The ray itself and the filter that decides what counts as a surface need a running
 * simulation and a booted engine and are NOT exercised here; what is, is the arithmetic
 * that combines the answer with the terrain height, because that is where the property
 * that gates the whole change lives - a flat city has to come out identical.
 */
public class HoverSurfaceProbeTests
{
    /**
     * Where a flat city puts a drivable surface: the fragment floor plane's top face,
     * the quarter floors, and a deck collider on a level stroke all land here.
     */
    private static float _flatCitySurface(float averageHeight)
        => averageHeight + MetaGen.CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE;


    /**
     * What the loop has always aimed at, and still does wherever nothing is built.
     */
    private static float _terrainHoverHeight(float averageHeight)
        => averageHeight + MetaGen.ClusterNavigationHeight;


    /**
     * THE ONE THAT GATES EVERYTHING.
     *
     * In a flat city the probe finds the floor plane on every single frame, so if the two
     * terms disagreed by so much as a decimetre the entire default game would move
     * vertically - and upwards, since the combination is a maximum. They do not disagree:
     * the clearance is the difference between the two constants that put the surface and
     * the old target where they are, so the ray term reproduces the terrain term exactly
     * and the maximum is a no-op.
     *
     * Note the assertion is on the TERM, not merely on the combined result. A clearance
     * that was too small would also leave the maximum picking the terrain height and pass
     * a test that only looked at the answer - right up to the first hillside, where the
     * road stands proud and the too-small clearance becomes the new command to descend
     * into it.
     */
    [Theory]
    [InlineData(0f)]
    [InlineData(20.1f)]
    [InlineData(-13.75f)]
    public void AFlatCityIsExactlyUnchanged(float averageHeight)
    {
        float surface = _flatCitySurface(averageHeight);
        float terrain = _terrainHoverHeight(averageHeight);

        Assert.Equal(terrain, surface + HoverSurfaceProbe.SurfaceClearance, 4);

        Assert.Equal(terrain, HoverSurfaceProbe.HoverTargetHeight(terrain, surface), 4);
    }


    /**
     * The clearance is derived and must stay derived. A hardcoded metre would be right
     * today and silently wrong the moment either constant is retuned.
     */
    [Fact]
    public void TheClearanceIsTheDifferenceBetweenTheTwoWorldConstants()
    {
        Assert.Equal(
            MetaGen.ClusterNavigationHeight - MetaGen.CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE,
            HoverSurfaceProbe.SurfaceClearance,
            4);
    }


    /**
     * The bug, as one assertion.
     *
     * A road on fill stands above the terrain under it. The loop used to aim at the
     * terrain plus 3 m, which on a 2.5 m fill is half a metre BELOW the road surface, so
     * it commanded a descent into the road for the length of the fill. Now the road wins.
     */
    [Fact]
    public void ARoadStandingOnFillRaisesTheTarget()
    {
        const float terrainUnderTheRoad = 100f;
        const float fill = 2.5f;

        float terrain = terrainUnderTheRoad + MetaGen.ClusterNavigationHeight;
        float road = terrainUnderTheRoad + fill + MetaGen.CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE;

        float target = HoverSurfaceProbe.HoverTargetHeight(terrain, road);

        Assert.Equal(road + HoverSurfaceProbe.SurfaceClearance, target, 4);
        Assert.True(target > road,
            $"the ship is aimed at {target} while the road it is driving on is at {road}, "
            + "so the loop is still commanding a descent into the road.");
    }


    /**
     * And the other sign, which is the one that keeps the ship out of the hillside. A
     * road in a CUTTING is below the terrain beside it, and the ray finds it; taking it
     * would fly the ship down into the cutting walls the terrain sample is warning about.
     * The maximum refuses.
     */
    [Fact]
    public void ARoadInACuttingDoesNotLowerTheTarget()
    {
        const float terrainBesideTheRoad = 100f;
        const float cut = 6f;

        float terrain = terrainBesideTheRoad + MetaGen.ClusterNavigationHeight;
        float road = terrainBesideTheRoad - cut + MetaGen.CLUSTER_STREET_ABOVE_CLUSTER_AVERAGE;

        Assert.Equal(terrain, HoverSurfaceProbe.HoverTargetHeight(terrain, road), 4);
    }


    /**
     * The ray sees colliders and the terrain has none, so over open country it reports
     * nothing at all - as it does off the edge of the world, and as it does over any part
     * of a city that was never built on. That is the normal case for most of the map, not
     * an error, and the answer there is the height the loop has always flown.
     */
    [Fact]
    public void NothingUnderTheShipMeansTheTerrainHeight()
    {
        Assert.Equal(37.5f, HoverSurfaceProbe.HoverTargetHeight(37.5f, null), 4);
        Assert.Equal(-4f, HoverSurfaceProbe.HoverTargetHeight(-4f, null), 4);
    }


    /**
     * What the ship holds its height over: the world, not the traffic in it.
     *
     * A house declares no mask at all and keeps the default Layers.All, so the test
     * cannot be a plain intersection with the moving layers - that would reject every
     * building in the city. It is whether the body is ONLY one of the moving kinds.
     */
    [Theory]
    [InlineData(CollisionProperties.Layers.Terrain)]
    [InlineData(CollisionProperties.Layers.StaticEnvironment)]
    [InlineData(CollisionProperties.Layers.MovableEnvironment)]
    [InlineData(CollisionProperties.Layers.All)]
    public void TheWorldIsASurface(CollisionProperties.Layers layers)
    {
        Assert.True(HoverSurfaceProbe.IsHoverSurface(layers));
    }


    /**
     * And nothing that walks, drives, is shot or is picked up is.
     *
     * The climb side of the servo keeps full authority by design, so a body that raises
     * the target does not merely nudge the ship - it is asked for LevelUpThrust. A
     * pedestrian passing under a parked ship would launch it.
     */
    [Theory]
    [InlineData(CollisionProperties.Layers.PlayerCharacter)]
    [InlineData(CollisionProperties.Layers.PlayerVehicle)]
    [InlineData(CollisionProperties.Layers.NpcCharacter)]
    [InlineData(CollisionProperties.Layers.NpcVehicle)]
    [InlineData(CollisionProperties.Layers.Npc)]
    [InlineData(CollisionProperties.Layers.AnyVehicle)]
    [InlineData(CollisionProperties.Layers.AnyWeapon)]
    [InlineData(CollisionProperties.Layers.Collectable)]
    [InlineData(CollisionProperties.Layers.QuestMarker)]
    [InlineData((CollisionProperties.Layers)0)]
    public void TrafficIsNotASurface(CollisionProperties.Layers layers)
    {
        Assert.False(HoverSurfaceProbe.IsHoverSurface(layers));
    }


    private static string _repoRoot()
    {
        string root = global::engine.GameRoot.PathTo("JoyceCode");
        Assert.False(String.IsNullOrEmpty(root), "could not locate the checkout");

        return Path.GetFullPath(Path.Combine(root, ".."));
    }


    /**
     * The cast itself, scanned, because it needs a booted engine and a simulation and
     * every way it can go wrong is silent.
     *
     * Three things have to hold and none of them fails loudly. The ray has to start ABOVE
     * the ship, or it starts inside the deck the ship is standing on and a convex shape
     * reports no hit from within - the probe would simply never see the road. It has to
     * reject the ship's own body, whose collision disc sits exactly on the ray. And the
     * cast has to stay the SYNC one, which takes the simulation lock itself; the async
     * form queues the work for another frame and would hand the servo a height from the
     * past.
     */
    [Fact]
    public void TheProbeCastsDownwardsFromAboveTheShipAndIgnoresIt()
    {
        string hover = File.ReadAllText(Path.Combine(
            _repoRoot(), "nogameCode/nogame/modules/playerhover/HoverController.cs"));

        Assert.Contains("RayCastSync(vOrigin, -Vector3.UnitY", hover);
        Assert.Contains("vShipPos.Y + SurfaceProbeAbove", hover);
        Assert.Contains("props.Entity == _eTarget", hover);

        Assert.True(hover.Contains("HoverSurfaceProbe.HoverTargetHeight"),
            "the hover loop no longer combines its terrain height with the surface the "
            + "ship is actually over, so it is back to aiming at the terrain under a road "
            + "that stands on fill - i.e. commanding a descent into the road.");
    }
}
