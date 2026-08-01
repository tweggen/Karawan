using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Xunit;
using engine.navigation;
using engine.streets;
using engine.tale;
using engine.world;
using builtin.modules.satnav.desc;

namespace JoyceCode.Tests.engine.tale;

/// <summary>
/// Regression tests for SpatialModel._computeStreetEntryCandidates and friends.
///
/// Background: TALE street_segment locations are created one per engine.streets.StreetPoint,
/// positioned at the junction CENTER (middle of the roadway). NPCs assigned to such a
/// location used to be placed exactly there, standing in the middle of the intersection.
/// The old code snapped the center onto the nearest pedestrian NavLane, but the nearest
/// pedestrian lane to a junction center is usually a crossing whose interior runs across
/// the roadway — so the projected point stayed inside the junction.
///
/// The fix snaps to pedestrian lane ENDPOINTS (NavJunctions = sidewalk corners) and,
/// without nav data, uses the street point's geometric section array (sidewalk corners,
/// already offset by half the street width). All corners of the junction are returned so
/// several NPCs bound to the same junction spread out instead of stacking on one point
/// (Location.EntryPositionFor).
/// </summary>
public class StreetEntryPositionTests
{
    private const float Eps = 0.001f;

    private static NavJunction Junction(Vector3 position) =>
        new NavJunction
        {
            Position = position,
            StartingLanes = new(),
            EndingLanes = new()
        };


    private static NavLane Lane(Vector3 start, Vector3 end, TransportationType types = TransportationType.Pedestrian) =>
        LaneBetween(Junction(start), Junction(end), types);


    /// <summary>
    /// A lane between two *shared* junction objects — models the real generator, where
    /// a sidewalk corner is one NavJunction referenced by several lanes.
    /// </summary>
    private static NavLane LaneBetween(NavJunction start, NavJunction end,
        TransportationType types = TransportationType.Pedestrian) =>
        new NavLane
        {
            Start = start,
            End = end,
            Length = Vector3.Distance(start.Position, end.Position),
            AllowedTypes = new TransportationTypeFlags(types)
        };


    private static NavCluster NavClusterWith(params NavLane[] lanes) => NavClusterWith(false, lanes);


    /// <summary>
    /// Hand-built NavCluster content. <paramref name="recompile"/> switches
    /// NavClusterContent.GetLanesNear between "return everything" (no octree yet)
    /// and the octree-backed spatial query.
    /// </summary>
    private static NavCluster NavClusterWith(bool recompile, params NavLane[] lanes)
    {
        var nc = new NavCluster { Id = "test-cluster" };
        var content = new NavClusterContent { Cluster = nc };
        foreach (var lane in lanes)
        {
            content.Lanes.Add(lane);
            if (!content.Junctions.Contains(lane.Start)) content.Junctions.Add(lane.Start);
            if (!content.Junctions.Contains(lane.End)) content.Junctions.Add(lane.End);
        }

        nc.Content = content;
        if (recompile) content.Recompile();
        return nc;
    }


    /// <summary>
    /// A street point with no strokes attached at all. GetSectionArray() returns an
    /// empty list for it, so the section-corner fallback degenerates to the junction
    /// center — which lets the nav-based paths be tested in isolation.
    /// </summary>
    private static StreetPoint DegenerateStreetPoint(Vector2 pos)
    {
        var sp = new StreetPoint();
        sp.SetPos(pos);
        return sp;
    }


    /// <summary>
    /// A realistic 4-way junction: four strokes radiating from the center street point.
    /// This is enough for StreetPoint.GetSectionArray() to compute real sidewalk corners
    /// (offset by half the street width) without booting the engine.
    /// </summary>
    private static StreetPoint CrossJunctionStreetPoint(Vector2 pos, float weight, float armLength)
    {
        var center = new StreetPoint();
        center.SetPos(pos);

        Vector2[] dirs =
        {
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(-1f, 0f),
            new Vector2(0f, -1f)
        };

        foreach (var dir in dirs)
        {
            var outer = new StreetPoint();
            outer.SetPos(pos + dir * armLength);

            var stroke = new Stroke
            {
                A = center,
                B = outer,
                Weight = weight,
                IsPrimary = true
            };

            center.AddStartingStroke(stroke);
            outer.AddEndingStroke(stroke);
        }

        return center;
    }


    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        var d = a - b;
        d.Y = 0f;
        return d.Length();
    }


    private static bool ContainsXZ(IEnumerable<Vector3> list, float x, float z) =>
        list.Any(p => MathF.Abs(p.X - x) < Eps && MathF.Abs(p.Z - z) < Eps);


    // ---------------------------------------------------------------------
    // Nav-based snapping
    // ---------------------------------------------------------------------

    /// <summary>
    /// The core regression: a crossing lane passes 8 m from the junction center while
    /// its endpoints (the sidewalk corners) are ~11.3 m away. The old projection-based
    /// code returned the 8 m point, still inside the roadway. Every candidate must be
    /// an actual lane endpoint.
    /// </summary>
    [Fact]
    public void Nav_SnapsToLaneEndpoint_NotLaneInterior()
    {
        var junctionCenter = new Vector3(100f, 5f, 100f);
        var sp = DegenerateStreetPoint(new Vector2(100f, 100f));

        // Crossing lane: interior passes at (100, 92) => 8 m from center,
        // endpoints at (92,92) / (108,92) => ~11.31 m from center.
        var crossing = Lane(new Vector3(92f, 0f, 92f), new Vector3(108f, 0f, 92f));
        var navCluster = NavClusterWith(crossing);

        var (candidates, isReachable) = SpatialModel._computeStreetEntryCandidates(
            navCluster, sp, Vector3.Zero, junctionCenter);

        Assert.True(isReachable);
        Assert.NotEmpty(candidates);

        // Not the perpendicular projection onto the lane.
        var projection = new Vector3(100f, junctionCenter.Y, 92f);
        Assert.All(candidates, c => Assert.True(HorizontalDistance(c, projection) > 1f,
            $"Candidate {c} is the lane projection {projection} — the fix is not in effect."));

        // Both lane endpoints, nothing else.
        Assert.Equal(2, candidates.Count);
        Assert.True(ContainsXZ(candidates, 92f, 92f));
        Assert.True(ContainsXZ(candidates, 108f, 92f));
    }


    [Fact]
    public void Nav_SnappedEntry_KeepsJunctionHeight()
    {
        var junctionCenter = new Vector3(100f, 5f, 100f);
        var sp = DegenerateStreetPoint(new Vector2(100f, 100f));

        // Lane endpoints deliberately at a different Y than the street height.
        var crossing = Lane(new Vector3(92f, -3.5f, 92f), new Vector3(108f, -3.5f, 92f));
        var navCluster = NavClusterWith(crossing);

        var (candidates, _) = SpatialModel._computeStreetEntryCandidates(
            navCluster, sp, Vector3.Zero, junctionCenter);

        Assert.All(candidates, c => Assert.Equal(junctionCenter.Y, c.Y, 3));
    }


    [Fact]
    public void Nav_EntryIsNeverTheJunctionCenter_WhenACornerIsInRange()
    {
        var junctionCenter = new Vector3(100f, 5f, 100f);
        var sp = DegenerateStreetPoint(new Vector2(100f, 100f));

        // Four crossings around the junction, as the lane generator emits them.
        var navCluster = NavClusterWith(
            Lane(new Vector3(92f, 0f, 92f), new Vector3(108f, 0f, 92f)),
            Lane(new Vector3(92f, 0f, 108f), new Vector3(108f, 0f, 108f)),
            Lane(new Vector3(92f, 0f, 92f), new Vector3(92f, 0f, 108f)),
            Lane(new Vector3(108f, 0f, 92f), new Vector3(108f, 0f, 108f)));

        var (candidates, isReachable) = SpatialModel._computeStreetEntryCandidates(
            navCluster, sp, Vector3.Zero, junctionCenter);

        Assert.True(isReachable);
        Assert.All(candidates, c => Assert.True(HorizontalDistance(c, junctionCenter) > 5f,
            $"Candidate {c} is only {HorizontalDistance(c, junctionCenter)} m from the junction center."));
    }


    [Fact]
    public void Nav_PicksTheClosestPedestrianCorner()
    {
        var junctionCenter = new Vector3(0f, 2f, 0f);
        var sp = DegenerateStreetPoint(Vector2.Zero);

        var navCluster = NavClusterWith(
            Lane(new Vector3(40f, 0f, 40f), new Vector3(60f, 0f, 40f)),
            Lane(new Vector3(-9f, 0f, -9f), new Vector3(-30f, 0f, -9f)),
            Lane(new Vector3(20f, 0f, 0f), new Vector3(20f, 0f, 30f)));

        var (candidates, isReachable) = SpatialModel._computeStreetEntryCandidates(
            navCluster, sp, Vector3.Zero, junctionCenter);

        Assert.True(isReachable);
        Assert.Equal(-9f, candidates[0].X, 3);
        Assert.Equal(-9f, candidates[0].Z, 3);
    }


    [Fact]
    public void Nav_IgnoresNonPedestrianLanes()
    {
        var junctionCenter = new Vector3(100f, 5f, 100f);
        var sp = DegenerateStreetPoint(new Vector2(100f, 100f));

        // A car lane whose endpoint is much closer than any pedestrian endpoint.
        var carLane = Lane(new Vector3(100f, 0f, 97f), new Vector3(100f, 0f, 60f), TransportationType.Car);
        var walkLane = Lane(new Vector3(92f, 0f, 92f), new Vector3(108f, 0f, 92f));
        var navCluster = NavClusterWith(carLane, walkLane);

        var (candidates, isReachable) = SpatialModel._computeStreetEntryCandidates(
            navCluster, sp, Vector3.Zero, junctionCenter);

        Assert.True(isReachable);
        Assert.False(ContainsXZ(candidates, 100f, 97f), "Picked a car-only lane endpoint.");
        Assert.All(candidates, c => Assert.Equal(92f, c.Z, 3));
    }


    [Fact]
    public void Nav_MultiTypeLaneCountsAsPedestrian()
    {
        var junctionCenter = new Vector3(0f, 1f, 0f);
        var sp = DegenerateStreetPoint(Vector2.Zero);

        var sharedLane = Lane(
            new Vector3(6f, 0f, 6f), new Vector3(30f, 0f, 6f),
            TransportationType.Pedestrian | TransportationType.Bicycle);
        var navCluster = NavClusterWith(sharedLane);

        var (candidates, isReachable) = SpatialModel._computeStreetEntryCandidates(
            navCluster, sp, Vector3.Zero, junctionCenter);

        Assert.True(isReachable);
        Assert.Equal(6f, candidates[0].X, 3);
        Assert.Equal(6f, candidates[0].Z, 3);
    }


    [Fact]
    public void Nav_OutOfRange_ReportsUnreachable()
    {
        var junctionCenter = new Vector3(0f, 5f, 0f);
        var sp = DegenerateStreetPoint(Vector2.Zero);

        // Every pedestrian endpoint is well beyond the default 50 m snap radius.
        var navCluster = NavClusterWith(
            Lane(new Vector3(400f, 0f, 400f), new Vector3(430f, 0f, 400f)));

        var (candidates, isReachable) = SpatialModel._computeStreetEntryCandidates(
            navCluster, sp, Vector3.Zero, junctionCenter);

        Assert.False(isReachable);
        // Degenerate street point => geometric fallback collapses to the center.
        Assert.Single(candidates);
        Assert.Equal(junctionCenter.X, candidates[0].X, 3);
        Assert.Equal(junctionCenter.Y, candidates[0].Y, 3);
        Assert.Equal(junctionCenter.Z, candidates[0].Z, 3);
    }


    [Fact]
    public void Nav_OnlyNonPedestrianLanes_ReportsUnreachable()
    {
        var junctionCenter = new Vector3(0f, 5f, 0f);
        var sp = DegenerateStreetPoint(Vector2.Zero);

        var navCluster = NavClusterWith(
            Lane(new Vector3(3f, 0f, 3f), new Vector3(30f, 0f, 3f), TransportationType.Car));

        var (candidates, isReachable) = SpatialModel._computeStreetEntryCandidates(
            navCluster, sp, Vector3.Zero, junctionCenter);

        Assert.False(isReachable);
        Assert.NotEmpty(candidates);
    }


    [Fact]
    public void Nav_SnapRadius_IsHonoured()
    {
        var junctionCenter = new Vector3(0f, 0f, 0f);
        var sp = DegenerateStreetPoint(Vector2.Zero);
        var navCluster = NavClusterWith(
            Lane(new Vector3(20f, 0f, 0f), new Vector3(40f, 0f, 0f)));

        var (inRange, reachableWide) = SpatialModel._computeStreetEntryCandidates(
            navCluster, sp, Vector3.Zero, junctionCenter, 25f);
        Assert.True(reachableWide);
        Assert.Equal(20f, inRange[0].X, 3);

        var (_, reachableNarrow) = SpatialModel._computeStreetEntryCandidates(
            navCluster, sp, Vector3.Zero, junctionCenter, 10f);
        Assert.False(reachableNarrow);
    }


    // ---------------------------------------------------------------------
    // Multi-candidate corners (corner-stacking fix)
    // ---------------------------------------------------------------------

    /// <summary>
    /// The four sidewalk corners of a 4-way junction, each shared by a sidewalk lane
    /// and a crossing lane, must all show up exactly once, nearest first.
    /// </summary>
    [Fact]
    public void Nav_FourWayJunction_YieldsAllFourCorners()
    {
        var junctionCenter = new Vector3(100f, 5f, 100f);
        var sp = DegenerateStreetPoint(new Vector2(100f, 100f));

        var sw = Junction(new Vector3(92f, 0f, 92f));
        var se = Junction(new Vector3(108f, 0f, 92f));
        var nw = Junction(new Vector3(92f, 0f, 108f));
        var ne = Junction(new Vector3(108f, 0f, 108f));

        var navCluster = NavClusterWith(
            LaneBetween(sw, se),
            LaneBetween(nw, ne),
            LaneBetween(sw, nw),
            LaneBetween(se, ne));

        var (candidates, isReachable) = SpatialModel._computeStreetEntryCandidates(
            navCluster, sp, Vector3.Zero, junctionCenter);

        Assert.True(isReachable);
        Assert.Equal(4, candidates.Count);
        Assert.True(ContainsXZ(candidates, 92f, 92f));
        Assert.True(ContainsXZ(candidates, 108f, 92f));
        Assert.True(ContainsXZ(candidates, 92f, 108f));
        Assert.True(ContainsXZ(candidates, 108f, 108f));

        // Nearest first.
        for (int i = 1; i < candidates.Count; i++)
        {
            Assert.True(
                HorizontalDistance(candidates[i - 1], junctionCenter) <=
                HorizontalDistance(candidates[i], junctionCenter) + Eps,
                "Candidates are not sorted nearest-first.");
        }
    }


    [Fact]
    public void EntryPositionFor_SpreadsNpcsAcrossCorners()
    {
        var junctionCenter = new Vector3(100f, 5f, 100f);
        var sp = DegenerateStreetPoint(new Vector2(100f, 100f));

        var sw = Junction(new Vector3(92f, 0f, 92f));
        var se = Junction(new Vector3(108f, 0f, 92f));
        var nw = Junction(new Vector3(92f, 0f, 108f));
        var ne = Junction(new Vector3(108f, 0f, 108f));
        var navCluster = NavClusterWith(
            LaneBetween(sw, se), LaneBetween(nw, ne), LaneBetween(sw, nw), LaneBetween(se, ne));

        var (candidates, _) = SpatialModel._computeStreetEntryCandidates(
            navCluster, sp, Vector3.Zero, junctionCenter);

        var loc = new Location
        {
            Id = 1,
            Type = "street_segment",
            Position = junctionCenter,
            EntryPosition = candidates[0],
            EntryCandidates = candidates
        };

        var picked = new[] { 0, 1, 2, 3 }.Select(loc.EntryPositionFor).ToList();
        Assert.Equal(4, picked.Distinct().Count());
        Assert.All(picked, p => Assert.Contains(p, candidates));
    }


    [Fact]
    public void EntryPositionFor_IsDeterministicPerNpc()
    {
        var loc = new Location
        {
            EntryCandidates = new List<Vector3>
            {
                new(1f, 0f, 1f), new(2f, 0f, 2f), new(3f, 0f, 3f)
            }
        };

        Assert.Equal(loc.EntryPositionFor(7), loc.EntryPositionFor(7));
        Assert.Equal(loc.EntryCandidates[1], loc.EntryPositionFor(7));   // 7 % 3 == 1
    }


    [Fact]
    public void EntryPositionFor_NegativeNpcId_StaysInRange()
    {
        var loc = new Location
        {
            EntryCandidates = new List<Vector3>
            {
                new(1f, 0f, 1f), new(2f, 0f, 2f), new(3f, 0f, 3f)
            }
        };

        foreach (int npcId in new[] { -1, -3, -7, int.MinValue + 1 })
        {
            var p = loc.EntryPositionFor(npcId);
            Assert.Contains(p, loc.EntryCandidates);
        }
    }


    [Fact]
    public void EntryPositionFor_NoCandidates_FallsBackToEntryPositionThenPosition()
    {
        var withEntry = new Location
        {
            Position = new Vector3(5f, 0f, 5f),
            EntryPosition = new Vector3(7f, 0f, 7f),
            EntryCandidates = null
        };
        Assert.Equal(withEntry.EntryPosition, withEntry.EntryPositionFor(3));

        var emptyList = new Location
        {
            Position = new Vector3(5f, 0f, 5f),
            EntryPosition = new Vector3(7f, 0f, 7f),
            EntryCandidates = new List<Vector3>()
        };
        Assert.Equal(emptyList.EntryPosition, emptyList.EntryPositionFor(3));

        var noEntry = new Location
        {
            Position = new Vector3(5f, 0f, 5f),
            EntryPosition = Vector3.Zero,
            EntryCandidates = null
        };
        Assert.Equal(noEntry.Position, noEntry.EntryPositionFor(3));
    }


    /// <summary>
    /// In the real generator a sidewalk corner is one NavJunction shared by the
    /// sidewalk lane and the crossing lane. It must be offered once, not twice.
    /// </summary>
    [Fact]
    public void Nav_SharedCornerIsDedupedAcrossLanes()
    {
        var junctionCenter = new Vector3(0f, 3f, 0f);
        var sp = DegenerateStreetPoint(Vector2.Zero);

        var corner = Junction(new Vector3(8f, 0f, 8f));
        var sidewalkEnd = Junction(new Vector3(8f, 0f, 40f));
        var crossingEnd = Junction(new Vector3(-8f, 0f, 8f));

        var navCluster = NavClusterWith(
            LaneBetween(corner, sidewalkEnd),
            LaneBetween(corner, crossingEnd),
            // Same corner once more, this time as the lane's End.
            LaneBetween(Junction(new Vector3(8f, 0f, -40f)), corner));

        var (candidates, _) = SpatialModel._computeStreetEntryCandidates(
            navCluster, sp, Vector3.Zero, junctionCenter);

        Assert.Equal(1, candidates.Count(c => MathF.Abs(c.X - 8f) < Eps && MathF.Abs(c.Z - 8f) < Eps));
    }


    [Fact]
    public void Nav_CornersBeyondSpreadTolerance_AreExcluded()
    {
        var junctionCenter = new Vector3(0f, 0f, 0f);
        var sp = DegenerateStreetPoint(Vector2.Zero);

        // Nearest corner at 6 m => tolerance window is 6 + 15 == 21 m.
        // 12 m is inside, 30 m is outside (but still inside the 50 m snapRadius).
        var navCluster = NavClusterWith(
            Lane(new Vector3(6f, 0f, 0f), new Vector3(6f, 0f, 45f)),
            Lane(new Vector3(0f, 0f, 12f), new Vector3(45f, 0f, 12f)),
            Lane(new Vector3(0f, 0f, 30f), new Vector3(45f, 0f, 30f)));

        var (candidates, isReachable) = SpatialModel._computeStreetEntryCandidates(
            navCluster, sp, Vector3.Zero, junctionCenter);

        Assert.True(isReachable);
        Assert.True(ContainsXZ(candidates, 6f, 0f));
        Assert.True(ContainsXZ(candidates, 0f, 12f));
        Assert.False(ContainsXZ(candidates, 0f, 30f),
            "A corner beyond nearest + CornerSpreadTolerance leaked into the candidates.");

        // ...and still nearest-first.
        Assert.Equal(6f, candidates[0].X, 3);
    }


    [Fact]
    public void Nav_CandidateCount_IsCappedAtSix()
    {
        var junctionCenter = new Vector3(0f, 2f, 0f);
        var sp = DegenerateStreetPoint(Vector2.Zero);

        // Octagon of eight equidistant corners at 10 m, connected by eight lanes.
        var corners = new NavJunction[8];
        for (int i = 0; i < 8; i++)
        {
            float a = i * MathF.PI / 4f;
            corners[i] = Junction(new Vector3(10f * MathF.Cos(a), 0f, 10f * MathF.Sin(a)));
        }

        var lanes = new NavLane[8];
        for (int i = 0; i < 8; i++)
            lanes[i] = LaneBetween(corners[i], corners[(i + 1) % 8]);

        var navCluster = NavClusterWith(lanes);

        var (candidates, isReachable) = SpatialModel._computeStreetEntryCandidates(
            navCluster, sp, Vector3.Zero, junctionCenter);

        Assert.True(isReachable);
        Assert.Equal(6, candidates.Count);
        Assert.Equal(6, candidates.Distinct().Count());
    }


    // ---------------------------------------------------------------------
    // No nav data: geometric section-corner fallback
    // ---------------------------------------------------------------------

    [Fact]
    public void NoNav_NullCluster_DegenerateStreetPoint_ReturnsCenterAndReachable()
    {
        var junctionCenter = new Vector3(100f, 5f, 100f);
        var sp = DegenerateStreetPoint(new Vector2(100f, 100f));

        var (candidates, isReachable) = SpatialModel._computeStreetEntryCandidates(
            null, sp, Vector3.Zero, junctionCenter);

        Assert.True(isReachable);
        Assert.Single(candidates);
        Assert.Equal(junctionCenter.X, candidates[0].X, 3);
        Assert.Equal(junctionCenter.Y, candidates[0].Y, 3);
        Assert.Equal(junctionCenter.Z, candidates[0].Z, 3);
    }


    [Fact]
    public void NoNav_EmptyLaneList_BehavesLikeNoNavData()
    {
        var junctionCenter = new Vector3(10f, 3f, -10f);
        var sp = DegenerateStreetPoint(new Vector2(10f, -10f));

        var emptyNav = NavClusterWith();

        var (candidates, isReachable) = SpatialModel._computeStreetEntryCandidates(
            emptyNav, sp, Vector3.Zero, junctionCenter);

        Assert.True(isReachable);
        Assert.Single(candidates);
        Assert.Equal(junctionCenter.X, candidates[0].X, 3);
        Assert.Equal(junctionCenter.Z, candidates[0].Z, 3);
    }


    /// <summary>
    /// Without nav data the entries must be sidewalk corners from the street point's
    /// section array — at least half a street width away from the junction center —
    /// never the center itself, and one per corner so NPCs can still spread out.
    /// </summary>
    [Fact]
    public void NoNav_WithStrokes_ReturnsAllSidewalkCornersOutsideTheRoadway()
    {
        var localPos = new Vector2(100f, 100f);
        const float weight = 1f;          // Stroke.StreetWidth() == 8 + 9*weight == 17
        const float halfStreetWidth = (8f + 9f * weight) / 2f;

        var sp = CrossJunctionStreetPoint(localPos, weight, 60f);
        var clusterPos = new Vector3(1000f, 0f, -2000f);
        var junctionCenter = new Vector3(localPos.X, 0f, localPos.Y) + clusterPos;
        junctionCenter.Y = 7.5f;

        var (candidates, isReachable) = SpatialModel._computeStreetEntryCandidates(
            null, sp, clusterPos, junctionCenter);

        Assert.True(isReachable);

        var sections = sp.GetSectionArray();
        Assert.Equal(4, sections.Count);            // 4-way junction => 4 corners
        Assert.Equal(sections.Count, candidates.Count);
        Assert.Equal(candidates.Count, candidates.Distinct().Count());

        for (int i = 0; i < sections.Count; i++)
        {
            var c = candidates[i];
            Assert.Equal(junctionCenter.Y, c.Y, 3);

            float dist = HorizontalDistance(c, junctionCenter);
            Assert.True(dist >= halfStreetWidth,
                $"Candidate {c} is only {dist} m from the junction center, inside the {halfStreetWidth * 2f} m roadway.");

            // Each candidate is its section corner pushed 1 m further outward.
            var raw = new Vector3(sections[i].X, junctionCenter.Y, sections[i].Y)
                      + new Vector3(clusterPos.X, 0f, clusterPos.Z);
            Assert.Equal(HorizontalDistance(raw, junctionCenter) + 1f, dist, 2);
        }
    }


    [Fact]
    public void NoNav_WithStrokes_AppliesClusterOffset()
    {
        var localPos = new Vector2(50f, -25f);
        var sp = CrossJunctionStreetPoint(localPos, 0.5f, 40f);

        var junctionLocal = new Vector3(localPos.X, 4f, localPos.Y);
        var (entryLocal, _) = SpatialModel._computeStreetEntryCandidates(
            null, sp, Vector3.Zero, junctionLocal);

        var clusterPos = new Vector3(-3000f, 0f, 777f);
        var junctionWorld = new Vector3(localPos.X + clusterPos.X, 4f, localPos.Y + clusterPos.Z);
        var (entryWorld, _) = SpatialModel._computeStreetEntryCandidates(
            null, sp, clusterPos, junctionWorld);

        Assert.Equal(entryLocal.Count, entryWorld.Count);
        for (int i = 0; i < entryLocal.Count; i++)
        {
            Assert.Equal(entryLocal[i].X + clusterPos.X, entryWorld[i].X, 2);
            Assert.Equal(entryLocal[i].Z + clusterPos.Z, entryWorld[i].Z, 2);
        }
    }


    /// <summary>
    /// Nav data present but out of range: the geometric corners are still used, but the
    /// location is flagged unreachable so role assignment can skip it.
    /// </summary>
    [Fact]
    public void NavOutOfRange_StillUsesGeometricCorners_ButUnreachable()
    {
        var localPos = new Vector2(0f, 0f);
        var sp = CrossJunctionStreetPoint(localPos, 1f, 50f);
        var junctionCenter = new Vector3(0f, 2f, 0f);

        var navCluster = NavClusterWith(
            Lane(new Vector3(500f, 0f, 500f), new Vector3(540f, 0f, 500f)));

        var (candidates, isReachable) = SpatialModel._computeStreetEntryCandidates(
            navCluster, sp, Vector3.Zero, junctionCenter);

        Assert.False(isReachable);
        Assert.Equal(4, candidates.Count);
        Assert.All(candidates, c => Assert.True(HorizontalDistance(c, junctionCenter) > 8.5f,
            $"Expected a geometric sidewalk corner, got {c}."));
    }


    /// <summary>
    /// A street point with a single stroke has no section array at all
    /// (StreetPoint._computeSectionArrayNoLock bails out below two strokes).
    /// It must degrade gracefully to the junction center rather than throw.
    /// </summary>
    [Fact]
    public void NoNav_SingleStrokeStreetPoint_FallsBackToCenter()
    {
        var center = new StreetPoint();
        center.SetPos(new Vector2(5f, 5f));
        var outer = new StreetPoint();
        outer.SetPos(new Vector2(45f, 5f));
        var stroke = new Stroke { A = center, B = outer, Weight = 1f };
        center.AddStartingStroke(stroke);
        outer.AddEndingStroke(stroke);

        var junctionCenter = new Vector3(5f, 3f, 5f);
        var (candidates, isReachable) = SpatialModel._computeStreetEntryCandidates(
            null, center, Vector3.Zero, junctionCenter);

        Assert.True(isReachable);
        Assert.Single(candidates);
        Assert.Equal(junctionCenter.X, candidates[0].X, 3);
        Assert.Equal(junctionCenter.Z, candidates[0].Z, 3);
    }


    [Fact]
    public void Deterministic_SameInputsGiveSameResult()
    {
        var sp = CrossJunctionStreetPoint(new Vector2(12f, 34f), 0.8f, 55f);
        var junctionCenter = new Vector3(12f, 6f, 34f);
        var navCluster = NavClusterWith(
            Lane(new Vector3(4f, 0f, 26f), new Vector3(4f, 0f, 60f)),
            Lane(new Vector3(20f, 0f, 26f), new Vector3(20f, 0f, 60f)));

        var (a, ra) = SpatialModel._computeStreetEntryCandidates(navCluster, sp, Vector3.Zero, junctionCenter);
        var (b, rb) = SpatialModel._computeStreetEntryCandidates(navCluster, sp, Vector3.Zero, junctionCenter);

        Assert.Equal(a, b);
        Assert.Equal(ra, rb);
    }


    // ---------------------------------------------------------------------
    // NavClusterContent.GetLanesNear (octree perf path)
    // ---------------------------------------------------------------------

    [Fact]
    public void GetLanesNear_WithoutRecompile_ReturnsAllLanes()
    {
        var near = Lane(new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f));
        var far = Lane(new Vector3(1000f, 0f, 1000f), new Vector3(1010f, 0f, 1000f));
        var navCluster = NavClusterWith(near, far);

        var results = new List<NavLane>();
        navCluster.Content.GetLanesNear(new Vector3(0f, 0f, 0f), 50f, results);

        Assert.Equal(2, results.Count);
        Assert.Contains(near, results);
        Assert.Contains(far, results);
    }


    [Fact]
    public void GetLanesNear_AfterRecompile_FiltersByDistance()
    {
        var near = Lane(new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f));
        var far = Lane(new Vector3(1000f, 0f, 1000f), new Vector3(1010f, 0f, 1000f));
        var navCluster = NavClusterWith(recompile: true, lanes: new[] { near, far });

        var results = new List<NavLane>();
        navCluster.Content.GetLanesNear(new Vector3(0f, 0f, 0f), 50f, results);

        Assert.Contains(near, results);
        Assert.DoesNotContain(far, results);

        var farResults = new List<NavLane>();
        navCluster.Content.GetLanesNear(new Vector3(1005f, 0f, 1000f), 50f, farResults);
        Assert.Contains(far, farResults);
        Assert.DoesNotContain(near, farResults);
    }


    /// <summary>
    /// The corner snapping must produce the same answer whether or not the octree
    /// has been built — GetLanesNear only narrows the candidate set.
    /// </summary>
    [Fact]
    public void Nav_CornerSnapping_WorksThroughTheOctree()
    {
        var junctionCenter = new Vector3(100f, 5f, 100f);
        var sp = DegenerateStreetPoint(new Vector2(100f, 100f));

        NavLane[] BuildLanes()
        {
            var sw = Junction(new Vector3(92f, 0f, 92f));
            var se = Junction(new Vector3(108f, 0f, 92f));
            var nw = Junction(new Vector3(92f, 0f, 108f));
            var ne = Junction(new Vector3(108f, 0f, 108f));
            return new[]
            {
                LaneBetween(sw, se), LaneBetween(nw, ne),
                LaneBetween(sw, nw), LaneBetween(se, ne),
                // A distant junction that must never contribute corners here.
                LaneBetween(Junction(new Vector3(900f, 0f, 900f)), Junction(new Vector3(920f, 0f, 900f)))
            };
        }

        var plain = NavClusterWith(recompile: false, lanes: BuildLanes());
        var octree = NavClusterWith(recompile: true, lanes: BuildLanes());

        var (plainCandidates, plainReachable) = SpatialModel._computeStreetEntryCandidates(
            plain, sp, Vector3.Zero, junctionCenter);
        var (octreeCandidates, octreeReachable) = SpatialModel._computeStreetEntryCandidates(
            octree, sp, Vector3.Zero, junctionCenter);

        Assert.True(plainReachable);
        Assert.True(octreeReachable);
        Assert.Equal(4, octreeCandidates.Count);
        Assert.Equal(
            plainCandidates.OrderBy(c => c.X).ThenBy(c => c.Z).ToList(),
            octreeCandidates.OrderBy(c => c.X).ThenBy(c => c.Z).ToList());
        Assert.False(ContainsXZ(octreeCandidates, 900f, 900f));
    }


    // ---------------------------------------------------------------------
    // ValidateReachability
    // ---------------------------------------------------------------------

    /// <summary>
    /// A crossing lane's INTERIOR passing near a street entry must not make that
    /// junction count as reachable — only actual sidewalk corners (lane endpoints) do.
    /// The projection-based check used to flip such locations back to reachable.
    /// ClusterDesc is only touched for its Name in trace strings, so a bare instance
    /// is enough (no I container involved).
    /// </summary>
    [Fact]
    public void ValidateReachability_StreetSegment_UsesEndpointsNotLaneInteriors()
    {
        // A single long pedestrian lane: its interior passes 10 m from the entry,
        // but both endpoints are ~200 m away.
        var longLane = Lane(new Vector3(-200f, 0f, 10f), new Vector3(200f, 0f, 10f));
        var navCluster = NavClusterWith(longLane);

        var entry = new Vector3(0f, 3f, 0f);

        var model = new SpatialModel();
        model.Locations.Add(new Location
        {
            Id = 0,
            Type = "street_segment",
            Position = entry,
            EntryPosition = entry,
            EntryCandidates = new List<Vector3> { entry },
            IsPedestrianReachable = true
        });
        model.Locations.Add(new Location
        {
            Id = 1,
            Type = "home",
            Position = entry,
            EntryPosition = entry,
            IsPedestrianReachable = true
        });
        model.BuildIndex();

        model.ValidateReachability(navCluster, new ClusterDesc { Name = "unit-test-cluster" });

        Assert.False(model.GetLocation(0).IsPedestrianReachable);
        // Buildings keep the projection-based semantics — a lane passing by the door
        // is genuinely walkable-to.
        Assert.True(model.GetLocation(1).IsPedestrianReachable);
    }


    [Fact]
    public void ValidateReachability_StreetSegment_ReachableWhenACornerIsInRange()
    {
        var crossing = Lane(new Vector3(-8f, 0f, -8f), new Vector3(8f, 0f, -8f));
        var navCluster = NavClusterWith(crossing);

        var model = new SpatialModel();
        model.Locations.Add(new Location
        {
            Id = 0,
            Type = "street_segment",
            Position = new Vector3(0f, 3f, 0f),
            EntryPosition = new Vector3(-8f, 3f, -8f),
            EntryCandidates = new List<Vector3> { new(-8f, 3f, -8f), new(8f, 3f, -8f) },
            IsPedestrianReachable = false
        });
        model.BuildIndex();

        model.ValidateReachability(navCluster, new ClusterDesc { Name = "unit-test-cluster" });

        Assert.True(model.GetLocation(0).IsPedestrianReachable);
    }


    [Fact]
    public void ValidateReachability_ZeroEntryPosition_IsUnreachable()
    {
        var navCluster = NavClusterWith(
            Lane(new Vector3(-8f, 0f, -8f), new Vector3(8f, 0f, -8f)));

        var model = new SpatialModel();
        model.Locations.Add(new Location
        {
            Id = 0,
            Type = "street_segment",
            Position = new Vector3(1f, 0f, 1f),
            EntryPosition = Vector3.Zero,
            IsPedestrianReachable = true
        });
        model.BuildIndex();

        model.ValidateReachability(navCluster, new ClusterDesc { Name = "unit-test-cluster" });

        Assert.False(model.GetLocation(0).IsPedestrianReachable);
    }
}
