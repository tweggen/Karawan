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

                            model.Locations.Add(new Location
                            {
                                Id = locationId++,
                                Type = locationType,
                                Position = building.GetCenter() + cluster.Pos,
                                Capacity = (int)MathF.Max(1, building.GetHeight() / 3f),
                                ShopType = shopType,
                                QuarterIndex = qi,
                                EstateIndex = ei
                            });
                        }
                    }

                    if (!hasShop)
                    {
                        float distFromCenter = building.GetCenter().Length();
                        float normalizedDist = distFromCenter / (cluster.Size / 2f);
                        string type = normalizedDist > 0.4f ? "home" : "workplace";

                        model.Locations.Add(new Location
                        {
                            Id = locationId++,
                            Type = type,
                            Position = building.GetCenter() + cluster.Pos,
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
            model.Locations.Add(new Location
            {
                Id = locId,
                Type = "street_segment",
                Position = sp.Pos3 + cluster.Pos,
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
}
