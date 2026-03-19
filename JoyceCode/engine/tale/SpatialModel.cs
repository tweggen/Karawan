using System;
using System.Collections.Generic;
using System.Numerics;
using engine.streets;
using engine.world;

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


    public static SpatialModel ExtractFrom(ClusterDesc cluster)
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
                        // Ensure cached buildings have tags assigned
                        // (buildings from cache may not have gone through QuarterGenerator._createBuildings())
                        if (!building.Tags.Contains("residential") &&
                            !building.Tags.Contains("office") &&
                            !building.Tags.Contains("warehouse"))
                        {
                            // Assign tags based on location attributes if missing
                            float livingIntensity = cluster.GetAttributeIntensity(
                                building.GetCenter() + cluster.Pos,
                                ClusterDesc.LocationAttributes.Living);
                            float downstairsIntensity = cluster.GetAttributeIntensity(
                                building.GetCenter() + cluster.Pos,
                                ClusterDesc.LocationAttributes.Downtown);
                            float industrialIntensity = cluster.GetAttributeIntensity(
                                building.GetCenter() + cluster.Pos,
                                ClusterDesc.LocationAttributes.Industrial);

                            if (industrialIntensity > 0.4f)
                                building.Tags.Add("warehouse");
                            else if (downstairsIntensity > 0.5f && livingIntensity < 0.7f)
                                building.Tags.Add("office");
                            else
                                building.Tags.Add("residential");
                        }

                        // Determine location type from building role tags
                        string type = "home";  // default
                        if (building.Tags.Contains("residential"))
                            type = "home";
                        else if (building.Tags.Contains("warehouse"))
                            type = "warehouse";
                        else if (building.Tags.Contains("office"))
                            type = "office";

                        var buildingCenter = building.GetCenter() + cluster.Pos;
                        var entryPos = new Vector3(buildingCenter.X, streetHeight, buildingCenter.Z);

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

            model.Locations.Add(new Location
            {
                Id = locId,
                Type = "street_segment",
                Position = pos,
                EntryPosition = pos,
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
}
