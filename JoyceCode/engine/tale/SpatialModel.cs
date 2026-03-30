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
    public List<Location> Locations = new();
    public List<Route> Routes = new();

    public int BuildingCount;
    public int ShopCount;
    public int StreetPointCount;

    private Dictionary<int, Location> _locationsById;
    private const float WalkingSpeedMetersPerMinute = 75f; // ~4.5 km/h


    public void BuildIndex()
    {
        _locationsById = new Dictionary<int, Location>(Locations.Count);
        foreach (var loc in Locations)
            _locationsById[loc.Id] = loc;
    }


    public Location GetLocation(int id)
    {
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

                            model.Locations.Add(new Location
                            {
                                Id = locationId++,
                                Type = locationType,
                                Position = buildingCenter,
                                EntryPosition = entryPos,
                                Capacity = (int)MathF.Max(1, building.GetHeight() / 3f),
                                ShopType = shopType,
                                QuarterIndex = qi,
                                EstateIndex = ei
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

                        model.Locations.Add(new Location
                        {
                            Id = locationId++,
                            Type = type,
                            Position = buildingCenter,
                            EntryPosition = entryPos,
                            Capacity = (int)MathF.Max(1, building.GetHeight() / 3f),
                            ShopType = null,
                            QuarterIndex = qi,
                            EstateIndex = ei
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
            pos.Y = streetHeight;

            // Try to find a point on a pedestrian NavLane near this street point
            Vector3 entryPos = pos;  // Fallback: use street point itself
            if (navCluster?.Content?.Lanes != null && navCluster.Content.Lanes.Count > 0)
            {
                // Find closest pedestrian lane to this street point
                float minDist = float.MaxValue;
                builtin.modules.satnav.desc.NavLane closestLane = null;
                Vector3 closestLanePoint = pos;

                foreach (var lane in navCluster.Content.Lanes)
                {
                    // Only consider pedestrian-accessible lanes
                    if (!lane.AllowedTypes.HasFlag(engine.navigation.TransportationType.Pedestrian))
                        continue;

                    // Find closest point on this lane
                    var laneStart = lane.Start.Position;
                    var laneEnd = lane.End.Position;
                    var laneVec = laneEnd - laneStart;
                    float laneLen2 = Vector3.Dot(laneVec, laneVec);

                    float t = 0f;
                    if (laneLen2 > 0.0001f)
                    {
                        t = Vector3.Dot(pos - laneStart, laneVec) / laneLen2;
                        t = Math.Clamp(t, 0f, 1f);
                    }

                    var lanePoint = laneStart + t * laneVec;
                    float dist = Vector3.Distance(pos, lanePoint);

                    if (dist < minDist)
                    {
                        minDist = dist;
                        closestLane = lane;
                        closestLanePoint = lanePoint;
                    }
                }

                if (closestLane != null && minDist < 50f)  // Use lane point if within 50m
                {
                    entryPos = closestLanePoint;
                    Trace($"Street location {locId}: pos={pos}, entryPos={entryPos} (on NavLane, dist={minDist:F1}m)");
                }
                else
                {
                    Trace($"Street location {locId}: pos={pos}, entryPos={pos} (no nearby pedestrian lane, dist to closest={minDist:F1}m)");
                }
            }
            else
            {
                Trace($"Street location {locId}: pos={pos}, entryPos={pos} (no NavCluster available)");
            }

            model.Locations.Add(new Location
            {
                Id = locId,
                Type = "street_segment",
                Position = pos,
                EntryPosition = entryPos,
                Capacity = 0,
                ShopType = null,
                QuarterIndex = -1,
                EstateIndex = -1
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
    /// Validate that all location entry points are reachable via the NavMap.
    /// Logs warnings for unreachable locations (entry point has no nearby NavJunctions).
    /// This helps identify stuck NPC issues early.
    /// </summary>
    public void ValidateReachability(builtin.modules.satnav.desc.NavCluster navCluster, ClusterDesc clusterDesc)
    {
        if (navCluster == null)
        {
            Trace($"⚠️ ValidateReachability: NavCluster null for cluster '{clusterDesc.Name}', skipping validation");
            return;
        }

        const float ReachabilityRadius = 10f; // Search radius for nearby NavJunctions

        int unreachableCount = 0;
        foreach (var loc in Locations)
        {
            if (loc.EntryPosition == Vector3.Zero)
            {
                Trace($"⚠️ UNREACHABLE_LOCATION: Cluster '{clusterDesc.Name}' Location {loc.Id} ({loc.Type}) has zero entry point");
                unreachableCount++;
                continue;
            }

            // Find NavJunctions near this entry point
            bool hasNearbyJunction = _hasNearbyNavJunction(navCluster, loc.EntryPosition, ReachabilityRadius);

            if (!hasNearbyJunction)
            {
                Trace($"⚠️ UNREACHABLE_LOCATION: Cluster '{clusterDesc.Name}' Location {loc.Id} ({loc.Type}) " +
                     $"entry point {loc.EntryPosition} has no NavJunctions within {ReachabilityRadius}m. " +
                     $"NPCs may become stuck trying to reach this location.");
                unreachableCount++;
            }
        }

        if (unreachableCount > 0)
        {
            Trace($"⚠️ ValidateReachability: {unreachableCount}/{Locations.Count} locations unreachable in cluster '{clusterDesc.Name}'");
        }
        else
        {
            Trace($"✓ ValidateReachability: All {Locations.Count} locations reachable in cluster '{clusterDesc.Name}'");
        }
    }


    /// <summary>
    /// Check if there's a NavJunction within radius of the given position.
    /// </summary>
    private static bool _hasNearbyNavJunction(builtin.modules.satnav.desc.NavCluster navCluster, Vector3 position, float radius)
    {
        if (navCluster?.Content?.Junctions == null)
            return true; // Assume reachable if NavCluster not loaded

        foreach (var junction in navCluster.Content.Junctions)
        {
            if (Vector3.Distance(position, junction.Position) <= radius)
                return true;
        }

        return false;
    }
}
