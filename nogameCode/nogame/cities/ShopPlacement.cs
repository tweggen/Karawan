using System;
using System.Collections.Generic;
using System.Numerics;
using builtin.tools;
using engine.world;
using engine.world.components;
using static engine.Logger;

namespace nogame.cities;


/**
 * Where a cluster's shops go: which shopfront of which building gets which kind of shop.
 * Tags each chosen shopfront "shop <IconCode>" - the tag the shop windows, the map and
 * TALE's SpatialModel read - and returns the placements, creating no entities.
 *
 * Split out of GenerateShopsOperator (KAR-479) so the placement can be measured on a
 * generated cluster without an engine; the operator creates the POIs from the result.
 * Every random number is drawn here, so the split changes no city's shops.
 */
public static class ShopPlacement
{
    public readonly record struct PlacedShop(
        engine.streets.Quarter Quarter, engine.streets.Building Building,
        Vector3 Local, int Try, MapIcon.IconCode Code);


    public static List<PlacedShop> Place(
        ClusterDesc clusterDesc, engine.streets.QuarterStore quarterStore, RandomSource rnd,
        float shopDistance, Func<Vector3, float> funcProbab, MapIcon.IconCode iconCode,
        bool traceDetail = false, IDictionary<string, int> discards = null)
    {
        var result = new List<PlacedShop>();
        float shopArea = shopDistance*shopDistance;
        float clusterArea = clusterDesc.Size * clusterDesc.Size;
        int nTries = (int)(clusterArea / shopArea);

        HashSet<Vector3> setPositions = new();

        for (int t = 0; t < nTries; t++)
        {
            bool isBadTry = false; 
            Vector2 v2TryLocal = new(
                    (rnd.GetFloat()-0.5f) * clusterDesc.Size,
                    (rnd.GetFloat()-0.5f) * clusterDesc.Size);
            Vector2 v2TryGlobal = v2TryLocal + clusterDesc.Pos2;

            /*
             * A sample COORDINATE, not a position: this asks a probability field how
             * shoppy a spot is. Deliberately still the cluster average, because feeding
             * it the terrain would make which shops exist depend on the ground under
             * them - a generation change, and not one this work is about.
             */
            float shopIntensity = funcProbab(new Vector3(v2TryGlobal.X, clusterDesc.AverageHeight, v2TryGlobal.Y));

            if (shopIntensity < 0.5f)
            {
                isBadTry = true;
                _count(discards, "outside zone");
                if (traceDetail) Trace($"Discarding shop {t} outside of shopping zone.");
                continue;
            }

            var quarter = quarterStore.GuessQuarter(v2TryGlobal);
            if (null == quarter)
            {
                isBadTry = true;
                _count(discards, "no quarter");
                if (traceDetail) Trace($"Discarding shop {t} because null quarter.");
                continue;
            }
            
            /*
             * Just randomly pick an estate.
             */
            var estates = quarter.GetEstates();
            if (null == estates || 0 == estates.Count)
            {
                isBadTry = true;
                _count(discards, "no estates");
                if (traceDetail) Trace($"Discarding shop {t} because no estates.");
                continue;
            }

            int nEstates = estates.Count;
            int estateStartIdx = (int)(rnd.GetFloat() * nEstates);
            IList<engine.streets.Building> buildings = null; 
            for (int estateOfs = 0; estateOfs < nEstates; estateOfs++)
            {
                int estateIdx = (estateStartIdx + estateOfs) % nEstates;
                engine.streets.Estate myEstate = estates[estateIdx];
                buildings = myEstate.GetBuildings();
                if (null != buildings && buildings.Count > 0)
                {
                    break;
                }
            }
            
            if (null == buildings || buildings.Count == 0)
            {
                isBadTry = true;
                _count(discards, "no building");
                if (traceDetail) Trace($"Discarding shop {t} because no building.");
                continue;
            }
            
            /*
             * TXWTODO: Iterate through the building's shop fronts until we found the first one
             * to use.
             *
             * ... we right now just take the first one.
             */
            int nBuildings = buildings.Count;
            int buildingStartIdx = (int)(rnd.GetFloat() * nBuildings);
            IList<engine.streets.ShopFront> shopFronts = null;

            /*
             * Which building the shopfront belongs to, and not merely which block: a block
             * carries several buildings since WP-O2 and each is founded on its own piece of
             * ground, so the storey a shop is on is a question about the building.
             */
            engine.streets.Building shopBuilding = null;
            for (int buildingOfs = 0; buildingOfs < nBuildings; buildingOfs++)
            {
                int buildingIdx = (buildingStartIdx + buildingOfs) % nBuildings;
                engine.streets.Building myBuilding = buildings[buildingIdx];
                shopFronts = myBuilding.GetShopFronts();
                if (null != shopFronts && shopFronts.Count > 0)
                {
                    shopBuilding = myBuilding;
                    break;
                }
            }

            if (null == shopFronts || shopFronts.Count == 0)
            {
                isBadTry = false;
                _count(discards, "no shopfronts");
                if (traceDetail) Trace($"Discarding shop {t} because no shop.");
                continue;
            }

            int nShops = shopFronts.Count;
            int shopStartIdx = (int)(rnd.GetFloat() * nShops);
            for (int shopOfs=0; shopOfs<nShops; shopOfs++)
            {
                int shopIdx = (shopStartIdx + shopOfs) % nShops;
                var myShopFront = shopFronts[shopIdx];
                /*
                 * A shopfront takes one shop. The tags are "shop <IconCode>"; this asked
                 * for a bare "shop", which no front ever carries, so a later kind of shop
                 * could land on a front an earlier one had taken - two POIs, one window.
                 */
                if (IsShop(myShopFront))
                {
                    // no bad try.
                    _count(discards, "front already tagged");
                    if (traceDetail) Trace($"Not using {t} because shopFront already tagged shop, trying to find another one for this try.");
                    continue;
                }

                // Reject up-front (before any side effects) so we never end up
                // with a shopfront tagged but no POI created.
                var myShopFrontPoints = myShopFront.GetPoints();
                if (null == myShopFrontPoints || myShopFrontPoints.Count < 2)
                {
                    _count(discards, "no front points");
                    if (traceDetail) Trace($"Discarding shop {t} because no shop front points.");
                    continue;
                }

                var nShopFronts = myShopFrontPoints.Count;
                int shopFrontIdx = (int)(rnd.GetFloat() * nShopFronts);
                Vector3 v3ShopLocal = (myShopFrontPoints[(shopFrontIdx) % nShopFronts] +
                                       myShopFrontPoints[(shopFrontIdx + 1) % nShopFronts]) / 2f;

                if (setPositions.Contains(v3ShopLocal))
                {
                    _count(discards, "same position");
                    if (traceDetail) Trace($"Discarding shop at {v3ShopLocal} because it already exists.");
                    continue;
                }

                if (!HasPedestrianAccess(v3ShopLocal, quarter))
                {
                    _count(discards, "no sidewalk");
                    if (traceDetail) Trace($"Discarding shop at {v3ShopLocal} because shopfront does not face a sidewalk.");
                    continue;
                }

                /*
                 * Tag the new shop.
                 */
                // TXWTODO: This is a wrong tag, and no meaningful set of tags either.
                string tagShop = $"shop {iconCode}";
                myShopFront.Tags.Add(tagShop);

                setPositions.Add(v3ShopLocal);

                result.Add(new PlacedShop(quarter, shopBuilding, v3ShopLocal, t, iconCode));
                break;
            }
            
            // We placed a shop if isBadTry==false
        }
        return result;
    }


    /** Whether a shopfront already holds a shop of any kind. */
    public static bool IsShop(engine.streets.ShopFront shopFront) =>
        shopFront.Tags.Contains($"shop {MapIcon.IconCode.Game2}")
        || shopFront.Tags.Contains($"shop {MapIcon.IconCode.Drink}")
        || shopFront.Tags.Contains($"shop {MapIcon.IconCode.Eat}");


    /*
     * Why tries fail, for measuring a cluster's shops (KAR-479); null counts nothing.
     */
    private static void _count(IDictionary<string, int> discards, string reason)
    {
        if (null == discards) return;
        discards.TryGetValue(reason, out int n);
        discards[reason] = n + 1;
    }


    /// <summary>
    /// Check if a shopfront midpoint sits next to a sidewalk. Sidewalks run along
    /// the perimeter of each quarter (built by GenerateNavMapOperator from delim
    /// segments), so a shopfront facing the quarter interior or wedged between
    /// neighbouring buildings has no pedestrian NavLane in front of it. Reusing
    /// the navmap's source data (quarter delims) avoids a sync wait on
    /// NavCluster.Content, which is built lazily and async.
    ///
    /// Mirrors the validity rule in GenerateNavMapOperator: a quarter with fewer
    /// than 3 delims gets no sidewalks at all.
    ///
    /// The lane runs along the kerb line (the delims) and the building wall stands
    /// the block's SidewalkWidth back from it, so a kerb-facing front is about that
    /// far away. The radius was a fixed 5 m (KAR-479): downtown blocks have 6 m of
    /// pavement, so it refused every kerb-facing front exactly where shops belong -
    /// a small city like Yelukhdidru got one shop. Narrower pavements keep 5 m.
    /// </summary>
    public static bool HasPedestrianAccess(Vector3 v3ShopLocal, engine.streets.Quarter quarter)
    {
        float sidewalkRadius = MathF.Max(5f, quarter.SidewalkWidth + 1f);
        float SidewalkRadiusSq = sidewalkRadius * sidewalkRadius;

        var delims = quarter.GetDelims();
        if (delims.Count < 3) return false;

        var v2Shop = new Vector2(v3ShopLocal.X, v3ShopLocal.Z);

        for (int i = 0; i < delims.Count; i++)
        {
            var a = delims[i].StartPoint;
            var b = delims[(i + 1) % delims.Count].StartPoint;

            var ab = b - a;
            float lenSq = Vector2.Dot(ab, ab);
            if (lenSq < 0.0001f) continue;

            var ap = v2Shop - a;
            float t = Math.Clamp(Vector2.Dot(ap, ab) / lenSq, 0f, 1f);
            var proj = a + t * ab;
            var d = v2Shop - proj;

            if (Vector2.Dot(d, d) <= SidewalkRadiusSq)
                return true;
        }

        return false;
    }
}
