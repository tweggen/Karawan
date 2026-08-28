using System;
using System.Collections.Generic;
using System.Numerics;
using engine.streets;
using engine.world;
using static engine.Logger;

namespace engine.tale;

public class Location
{
    public int Id;
    public string Type; // "home", "workplace", "shop", "social_venue", "street_segment"
    public Vector3 Position;
    public Vector3 EntryPosition; // Door/shop front position at street level
    public int Capacity;
    public string ShopType; // null if not a shop, else "Eat", "Drink", "Game2"
    public int QuarterIndex;
    public int EstateIndex;

    // True when EntryPosition has a pedestrian NavLane within reach. Defaults to true
    // so call sites without a NavCluster (bake, testbed) don't accidentally filter the
    // location out. Set to false in ExtractFrom / ValidateReachability when navigation
    // info is available and the entry point can't be reached on foot.
    public bool IsPedestrianReachable = true;

    // For street (junction) locations: all sidewalk corners around the junction.
    // EntryPosition is always EntryCandidates[0]. Null/empty for building locations
    // (single entry). Lets multiple NPCs bound to the same junction spread across
    // its corners instead of stacking on one point — pick with EntryPositionFor.
    public List<Vector3> EntryCandidates;

    /// <summary>
    /// The entry position an individual NPC should stand at. Distributes NPCs
    /// deterministically across EntryCandidates (sidewalk corners) when the
    /// location has several; otherwise behaves exactly like EntryPosition.
    /// </summary>
    public Vector3 EntryPositionFor(int npcId)
    {
        if (EntryCandidates != null && EntryCandidates.Count > 0)
        {
            int n = EntryCandidates.Count;
            return EntryCandidates[((npcId % n) + n) % n];
        }
        return EntryPosition != Vector3.Zero ? EntryPosition : Position;
    }
}

public class Route
{
    public int OriginId;
    public int DestinationId;
    public float TravelTimeMinutes;
    public List<int> StreetSegmentIds;
}

public class SpatialModel
{
    private static readonly engine.Dc _dc = engine.Dc.SpatialModel;

    public List<Location> Locations = new();
    public List<Route> Routes = new();

    public int BuildingCount;
    public int ShopCount;
    public int StreetPointCount;

    private Dictionary<int, Location> _locationsById;
    private const float WalkingSpeedMetersPerMinute = 75f; // ~4.5 km/h

    // Single source of truth for "a pedestrian lane counts as within reach".
    // Used by entry-point snapping, street corner collection and ValidateReachability.
    private const float PedestrianSnapRadius = 50f;


    public void BuildIndex()
    {
        _locationsById = new Dictionary<int, Location>(Locations.Count);
        foreach (var loc in Locations)
            _locationsById[loc.Id] = loc;
    }


    public Location GetLocation(int id)
    {
        if (_locationsById == null) BuildIndex();
        return _locationsById.TryGetValue(id, out var loc) ? loc : null;
    }


    /// <summary>
    /// Travel time in minutes between two locations using Euclidean distance.
    /// For Tier 3 background simulation this is sufficient; actual pathfinding
    /// is only needed for Tier 2/1 visible NPCs.
    /// </summary>
    public float GetTravelTime(int fromId, int toId)
    {
        if (fromId == toId) return 0f;
        var from = GetLocation(fromId);
        var to = GetLocation(toId);
        if (from == null || to == null) return 0f;
        return Vector3.Distance(from.Position, to.Position) / WalkingSpeedMetersPerMinute;
    }


    public int FindNearestOfType(int fromId, string type, string shopType = null)
    {
        var from = GetLocation(fromId);
        if (from == null) return -1;

        float bestDist = float.MaxValue;
        int bestId = -1;

        foreach (var loc in Locations)
        {
            if (loc.Type != type) continue;
            if (shopType != null && loc.ShopType != shopType) continue;
            float dist = Vector3.DistanceSquared(from.Position, loc.Position);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestId = loc.Id;
            }
        }

        return bestId;
    }


    public static SpatialModel ExtractFrom(ClusterDesc cluster, builtin.modules.satnav.desc.NavCluster navCluster = null)
    {
        var model = new SpatialModel();
        int locationId = 0;

        var quarterStore = cluster.QuarterStore();
        var strokeStore = cluster.StrokeStore();
        var streetPointToLocation = new Dictionary<int, int>();

        /*
         * Quarters and estates are traced on the ground and still sit at the cluster
         * average, so they keep this. Street points below ask the height source per
         * junction instead, which is exact.
         */
        var heightSource = cluster.StreetHeightSource;
        float streetHeight = cluster.AverageHeight + MetaGen.ClusterStreetHeight + MetaGen.QuarterSidewalkOffset;

        var quarters = quarterStore.GetQuarters();
        for (int qi = 0; qi < quarters.Count; qi++)
        {
            var quarter = quarters[qi];
            if (quarter.IsInvalid()) continue;

            var estates = quarter.GetEstates();
            for (int ei = 0; ei < estates.Count; ei++)
            {
                var estate = estates[ei];
                var buildings = estate.GetBuildings();

                for (int bi = 0; bi < buildings.Count; bi++)
                {
                    var building = buildings[bi];
                    model.BuildingCount++;

                    var shopFronts = building.GetShopFronts();
                    bool hasShop = false;

                    foreach (var sf in shopFronts)
                    {
                        string shopType = null;
                        if (sf.Tags.Contains("shop Game2"))
                            shopType = "Game2";
                        else if (sf.Tags.Contains("shop Drink"))
                            shopType = "Drink";
                        else if (sf.Tags.Contains("shop Eat"))
                            shopType = "Eat";

                        if (shopType != null)
                        {
                            hasShop = true;
                            model.ShopCount++;

                            string locationType = shopType switch
                            {
                                "Drink" => "social_venue",
                                "Eat" => "social_venue",
                                "Game2" => "shop",
                                _ => "shop"
                            };

                            var buildingCenter = building.GetCenter() + cluster.Pos;
                            buildingCenter.Y = streetHeight;
                            var entryPos = ComputeShopEntryPosition(sf, cluster, streetHeight);
                            var (snappedEntry, isReachable) = _snapToPedestrianLane(navCluster, entryPos);

                            model.Locations.Add(new Location
                            {
                                Id = locationId++,
                                Type = locationType,
                                Position = buildingCenter,
                                EntryPosition = snappedEntry,
                                Capacity = (int)MathF.Max(1, building.GetHeight() / 3f),
                                ShopType = shopType,
                                QuarterIndex = qi,
                                EstateIndex = ei,
                                IsPedestrianReachable = isReachable
                            });
                        }
                    }

                    if (!hasShop)
                    {
                        // Determine location type from building role tags
                        string type = "home";  // default
                        if (building.Tags.Contains("residential"))
                            type = "home";
                        else if (building.Tags.Contains("warehouse"))
                            type = "warehouse";
                        else if (building.Tags.Contains("office"))
                            type = "office";

                        var buildingCenter = building.GetCenter() + cluster.Pos;
                        buildingCenter.Y = streetHeight;

                        // Compute entry position on building's edge (perimeter point)
                        // If building has points, use the first point; otherwise fall back to center
                        var buildingPoints = building.GetPoints();
                        Vector3 entryPos;
                        if (buildingPoints.Count > 0)
                        {
                            // Use first point on building perimeter, offset towards center slightly
                            var perimeterPoint = buildingPoints[0] + cluster.Pos;
                            perimeterPoint.Y = 0; // Reset Y to cluster local height
                            var direction = Vector3.Normalize(buildingCenter - perimeterPoint);
                            // Move a small distance from perimeter towards center to avoid exact edge
                            var offset = direction * 2f; // 2 meter offset from edge
                            entryPos = new Vector3(perimeterPoint.X + offset.X, streetHeight, perimeterPoint.Z + offset.Z);
                        }
                        else
                        {
                            // Fallback: use building center if no points available
                            entryPos = buildingCenter;
                        }

                        var (snappedEntry, isReachable) = _snapToPedestrianLane(navCluster, entryPos);

                        model.Locations.Add(new Location
                        {
                            Id = locationId++,
                            Type = type,
                            Position = buildingCenter,
                            EntryPosition = snappedEntry,
                            Capacity = (int)MathF.Max(1, building.GetHeight() / 3f),
                            ShopType = null,
                            QuarterIndex = qi,
                            EstateIndex = ei,
                            IsPedestrianReachable = isReachable
                        });
                    }
                }
            }
        }

        var streetPoints = strokeStore.GetStreetPoints();
        model.StreetPointCount = streetPoints.Count;
        foreach (var sp in streetPoints)
        {
            int locId = locationId++;
            streetPointToLocation[sp.Id] = locId;
            var pos = sp.Pos3 + cluster.Pos;

            /*
             * Y is set outright rather than accumulated, so the deck height goes here
             * rather than onto Pos3 - which is the planar octree key and must stay so.
             */
            pos.Y = heightSource.GroundHeightAt(sp)
                    + MetaGen.ClusterStreetHeight
                    + MetaGen.QuarterSidewalkOffset
                    + sp.LevelElevation;

            // The street point itself is the junction center — the middle of the
            // roadway. NPCs standing there (street storylets, drifter homes, ...)
            // must be moved to a sidewalk corner instead. Note that
            // _snapToPedestrianLane is NOT suitable here: the nearest pedestrian
            // lane to a junction center is usually a crossing, whose interior is
            // also in the roadway.
            var (entryCandidates, isReachable) = _computeStreetEntryCandidates(navCluster, sp, cluster.Pos, pos);

            model.Locations.Add(new Location
            {
                Id = locId,
                Type = "street_segment",
                Position = pos,
                EntryPosition = entryCandidates[0],
                EntryCandidates = entryCandidates,
                Capacity = 0,
                ShopType = null,
                QuarterIndex = -1,
                EstateIndex = -1,
                IsPedestrianReachable = isReachable
            });
        }
        
        var strokes = strokeStore.GetStrokes();
        foreach (var stroke in strokes)
        {
            if (stroke.A == null || stroke.B == null) continue;
            if (!streetPointToLocation.TryGetValue(stroke.A.Id, out int originId)) continue;
            if (!streetPointToLocation.TryGetValue(stroke.B.Id, out int destId)) continue;

            float distMeters = stroke.Length;
            float travelTimeMinutes = distMeters / WalkingSpeedMetersPerMinute;

            model.Routes.Add(new Route
            {
                OriginId = originId,
                DestinationId = destId,
                TravelTimeMinutes = travelTimeMinutes,
                StreetSegmentIds = new List<int> { stroke.Sid }
            });

            model.Routes.Add(new Route
            {
                OriginId = destId,
                DestinationId = originId,
                TravelTimeMinutes = travelTimeMinutes,
                StreetSegmentIds = new List<int> { stroke.Sid }
            });
        }

        model.BuildIndex();

        // Log location distribution for debugging
        var homeCount = model.Locations.FindAll(l => l.Type == "home").Count;
        var officeCount = model.Locations.FindAll(l => l.Type == "office").Count;
        var warehouseCount = model.Locations.FindAll(l => l.Type == "warehouse").Count;
        var shopCount = model.Locations.FindAll(l => l.Type == "shop").Count;
        var socialVenueCount = model.Locations.FindAll(l => l.Type == "social_venue").Count;
        var streetCount = model.Locations.FindAll(l => l.Type == "street_segment").Count;
        Logger.Trace($"SPATIAL MODEL for cluster: {homeCount} homes, {officeCount} offices, {warehouseCount} warehouses, {shopCount} shops, {socialVenueCount} social_venues, {streetCount} streets. Total: {model.Locations.Count}");

        return model;
    }


    /// <summary>
    /// Snap a position to the closest point on the nearest pedestrian-accessible NavLane.
    /// Returns the snapped point and a flag indicating whether snapping succeeded
    /// (i.e. a pedestrian lane was found within <paramref name="snapRadius"/>).
    /// When <paramref name="navCluster"/> is null (bake / testbed paths) or no pedestrian
    /// lane is in range, returns the original position and reports it as reachable so the
    /// caller doesn't accidentally drop locations in environments without nav data.
    /// </summary>
    private static (Vector3 snapped, bool isReachable) _snapToPedestrianLane(
        builtin.modules.satnav.desc.NavCluster navCluster,
        Vector3 position,
        float snapRadius = PedestrianSnapRadius)
    {
        if (navCluster?.Content?.Lanes == null || navCluster.Content.Lanes.Count == 0)
            return (position, true);

        float minDist = float.MaxValue;
        Vector3 closestLanePoint = position;

        // Only scan lanes near the position (octree-backed once Recompile ran).
        var nearbyLanes = new List<builtin.modules.satnav.desc.NavLane>();
        navCluster.Content.GetLanesNear(position, snapRadius, nearbyLanes);

        foreach (var lane in nearbyLanes)
        {
            if (!lane.AllowedTypes.HasFlag(engine.navigation.TransportationType.Pedestrian))
                continue;

            var laneStart = lane.Start.Position;
            var laneEnd = lane.End.Position;
            var laneVec = laneEnd - laneStart;
            float laneLen2 = Vector3.Dot(laneVec, laneVec);

            float t = 0f;
            if (laneLen2 > 0.0001f)
            {
                t = Vector3.Dot(position - laneStart, laneVec) / laneLen2;
                t = Math.Clamp(t, 0f, 1f);
            }

            var lanePoint = laneStart + t * laneVec;
            float dist = Vector3.Distance(position, lanePoint);

            if (dist < minDist)
            {
                minDist = dist;
                closestLanePoint = lanePoint;
            }
        }

        if (minDist <= snapRadius)
            return (closestLanePoint, true);

        return (position, false);
    }


    // Nav-based street corners: keep every corner no farther than the nearest
    // one plus this tolerance, so all corners of the same junction qualify but
    // corners of the next junction down the street usually don't.
    private const float CornerSpreadTolerance = 15f;
    private const int MaxEntryCandidates = 6;

    /// <summary>
    /// Compute the entry (standing) positions for a street point (junction)
    /// location. A junction center is in the middle of the roadway, so NPCs must
    /// never be placed there. Preference order:
    /// 1. Pedestrian NavLane *endpoints* (sidewalk corners) around the junction.
    ///    Endpoints are used instead of lane projections because the lane nearest
    ///    to a junction center is typically a crossing, whose interior crosses
    ///    the roadway. All corners within CornerSpreadTolerance of the nearest
    ///    are returned (nearest first) so NPCs can spread across the junction.
    /// 2. Without nav data: the street point's section array — the geometric
    ///    sidewalk corner points, already offset by half the street width —
    ///    each nudged 1 m further away from the junction center.
    /// 3. The raw junction center only if neither is available.
    /// The returned list is never empty; index 0 is the canonical EntryPosition.
    /// Reachability semantics mirror _snapToPedestrianLane: reachable defaults to
    /// true when no nav data exists so bake/testbed callers don't drop locations.
    /// </summary>
    internal static (List<Vector3> candidates, bool isReachable) _computeStreetEntryCandidates(
        builtin.modules.satnav.desc.NavCluster navCluster,
        StreetPoint sp,
        Vector3 clusterPos,
        Vector3 junctionCenter,
        float snapRadius = PedestrianSnapRadius)
    {
        bool haveNav = navCluster?.Content?.Lanes != null && navCluster.Content.Lanes.Count > 0;

        if (haveNav)
        {
            var corners = _pedestrianCornersNear(navCluster, junctionCenter, snapRadius);
            if (corners.Count > 0)
            {
                for (int i = 0; i < corners.Count; i++)
                {
                    var c = corners[i];
                    c.Y = junctionCenter.Y;
                    corners[i] = c;
                }
                return (corners, true);
            }

            // Nav data exists but no pedestrian corner in range: geometrically
            // computed corners, flagged unreachable so role assignment skips it.
            return (_sectionCornersOrCenter(sp, clusterPos, junctionCenter), false);
        }

        return (_sectionCornersOrCenter(sp, clusterPos, junctionCenter), true);
    }


    /// <summary>
    /// Distinct pedestrian lane endpoints within snapRadius of the position,
    /// nearest first, trimmed to the nearest-corner distance plus
    /// CornerSpreadTolerance and capped at MaxEntryCandidates. Empty when no
    /// pedestrian endpoint is in range.
    /// </summary>
    private static List<Vector3> _pedestrianCornersNear(
        builtin.modules.satnav.desc.NavCluster navCluster,
        Vector3 position,
        float snapRadius)
    {
        var nearbyLanes = new List<builtin.modules.satnav.desc.NavLane>();
        navCluster.Content.GetLanesNear(position, snapRadius, nearbyLanes);

        // Dedupe endpoints by ~0.5 m X/Z grid cell — sidewalk corners are shared
        // by several lanes (sidewalk + crossing) and must only be counted once.
        // (Cell-based, so this merges coincident corners, not a true distance
        // threshold — good enough for real 8-16 m junction geometry.)
        var seen = new HashSet<(int, int)>();
        var corners = new List<(Vector3 pos, float dist)>();

        void addEndpoint(Vector3 p)
        {
            float dist = Vector3.Distance(position, p);
            if (dist > snapRadius) return;
            var key = ((int)MathF.Round(p.X * 2f), (int)MathF.Round(p.Z * 2f));
            if (!seen.Add(key)) return;
            corners.Add((p, dist));
        }

        foreach (var lane in nearbyLanes)
        {
            if (!lane.AllowedTypes.HasFlag(engine.navigation.TransportationType.Pedestrian))
                continue;
            addEndpoint(lane.Start.Position);
            addEndpoint(lane.End.Position);
        }

        if (corners.Count == 0)
            return new List<Vector3>();

        corners.Sort((a, b) => a.dist.CompareTo(b.dist));
        float maxDist = corners[0].dist + CornerSpreadTolerance;

        var result = new List<Vector3>();
        foreach (var (p, dist) in corners)
        {
            if (dist > maxDist) break;
            result.Add(p);
            if (result.Count >= MaxEntryCandidates) break;
        }
        return result;
    }


    /// <summary>
    /// The sidewalk corners from the street point's section array (offset by
    /// half street width during street generation), each nudged 1 m further
    /// outward. Falls back to the junction center for degenerate street points.
    /// Never returns an empty list.
    /// </summary>
    private static List<Vector3> _sectionCornersOrCenter(StreetPoint sp, Vector3 clusterPos, Vector3 junctionCenter)
    {
        List<Vector2> sections;
        try
        {
            sections = sp.GetSectionArray();
        }
        catch (Exception e)
        {
            // Degenerate street point (e.g. no strokes attached). Falls back to the
            // junction center — trace it, so a regression to mid-junction NPCs is visible.
            Trace(_dc, $"Street point {sp.Id}: GetSectionArray failed ({e.Message}), entry stays at junction center.");
            return new List<Vector3> { junctionCenter };
        }
        if (sections == null || sections.Count == 0)
            return new List<Vector3> { junctionCenter };

        var result = new List<Vector3>(sections.Count);
        foreach (var s in sections)
        {
            var corner = new Vector3(s.X, 0f, s.Y) + clusterPos;
            corner.Y = junctionCenter.Y;

            var outward = corner - junctionCenter;
            outward.Y = 0f;
            if (outward.LengthSquared() > 0.01f)
                corner += Vector3.Normalize(outward) * 1f;

            result.Add(corner);
            if (result.Count >= MaxEntryCandidates) break;
        }
        return result;
    }


    private static Vector3 ComputeShopEntryPosition(ShopFront shopFront, ClusterDesc cluster, float streetHeight)
    {
        var points = shopFront.GetPoints();
        if (points.Count == 0)
            return cluster.Pos;

        Vector3 entry;
        if (points.Count >= 2)
        {
            // Use midpoint of first two points
            entry = (points[0] + points[1]) / 2f;
        }
        else
        {
            // Single point: use it directly
            entry = points[0];
        }

        // Project to world space and street height
        entry += cluster.Pos;
        entry.Y = streetHeight;
        return entry;
    }


    /// <summary>
    /// Re-check pedestrian reachability for every location and update <see cref="Location.IsPedestrianReachable"/>.
    /// ExtractFrom already does this when a NavCluster is supplied; this pass is the safety net for
    /// locations built without nav info, and also surfaces a per-cluster summary for diagnostics.
    /// </summary>
    public void ValidateReachability(builtin.modules.satnav.desc.NavCluster navCluster, ClusterDesc clusterDesc)
    {
        if (navCluster == null)
        {
            Trace(_dc, $"⚠️ ValidateReachability: NavCluster null for cluster '{clusterDesc.Name}', skipping validation");
            return;
        }

        int unreachableCount = 0;
        foreach (var loc in Locations)
        {
            if (loc.EntryPosition == Vector3.Zero)
            {
                Trace(_dc, $"⚠️ UNREACHABLE_LOCATION: Cluster '{clusterDesc.Name}' Location {loc.Id} ({loc.Type}) has zero entry point");
                loc.IsPedestrianReachable = false;
                unreachableCount++;
                continue;
            }

            // Street (junction) locations are validated with the same endpoint-based
            // check ExtractFrom used — the projection-based _snapToPedestrianLane
            // would flip "unreachable" street corners back to reachable whenever a
            // crossing lane's interior passes within range of the geometric corner.
            bool isReachable;
            if (loc.Type == "street_segment")
            {
                isReachable = _pedestrianCornersNear(navCluster, loc.EntryPosition, PedestrianSnapRadius).Count > 0;
            }
            else
            {
                (_, isReachable) = _snapToPedestrianLane(navCluster, loc.EntryPosition);
            }
            loc.IsPedestrianReachable = isReachable;

            if (!isReachable)
            {
                Trace(_dc, $"⚠️ UNREACHABLE_LOCATION: Cluster '{clusterDesc.Name}' Location {loc.Id} ({loc.Type}) " +
                     $"entry point {loc.EntryPosition} has no pedestrian NavLane within reach.");
                unreachableCount++;
            }
        }

        if (unreachableCount > 0)
        {
            Trace(_dc, $"⚠️ ValidateReachability: {unreachableCount}/{Locations.Count} locations unreachable in cluster '{clusterDesc.Name}'");
        }
        else
        {
            Trace(_dc, $"✓ ValidateReachability: All {Locations.Count} locations reachable in cluster '{clusterDesc.Name}'");
        }
    }
}
