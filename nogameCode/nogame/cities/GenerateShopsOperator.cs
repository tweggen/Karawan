
using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;
using builtin.tools;
using engine;
using engine.draw;
using engine.joyce;
using engine.world;
using static engine.Logger;

namespace nogame.cities;

/**
 * Generate a standard set of shops.
 *
 * These shops create points of interest, which may be visible in a map
 * and may have a mission target later in a game.
 * They also may trigger an action if touched.
 * For visualization, they are associateed with a specific shopfront
 * on a building to make them stand out between other shops.
 */
class GenerateShopsOperator : IClusterOperator
{
    private static RandomSource _rnd = new("generateshops");

    /**
     * The map we shall render on
     */
    public uint MapCameraMask { get; set; } = 0x00800000;

    public bool TraceMe { get; set; } = true;

    public void ClusterOperatorApply(ClusterDesc clusterDesc)
    {
        /*
         * This is a stupidly simple imple100mentation, chosing just about one shop 100m100m of cluster size
         * in the shopping zone as a shop.
         *
         * We do a monte carlo approach. If the shops should be 100m apart on average, we
         * shoot area / (100*109m^2) times, applying a probability if we hit a shopping zone.
         */
        float shopDistance = 50f;
        float shopArea = shopDistance*shopDistance;
        float clusterArea = clusterDesc.Size * clusterDesc.Size;
        int nTries = (int)(clusterArea / shopArea);

        for (int t = 0; t < nTries; t++)
        {
            Vector2 v2Try = new(
                (_rnd.GetFloat()-0.5f) * clusterDesc.Size,
                (_rnd.GetFloat()-0.5f) * clusterDesc.Size);
            float shopIntensity =
                clusterDesc.GetAttributeIntensity(
                    new(v2Try.X, 0f, v2Try.Y), 
                    ClusterDesc.LocationAttributes.Shopping);
            if (shopIntensity < 0.5f)
            {
                if (TraceMe) Trace($"Discarding shop {t} outside of shopping zone.");
                continue;
            }

            var quarter = clusterDesc.GuessQuarter(v2Try);
            if (null == quarter)
            {
                if (TraceMe) Trace($"Discarding shop {t} because null quarter.");
                continue;
            }
            
            /*
             * Just randomly pick an estate.
             */
            var estates = quarter.GetEstates();
            if (null == estates || 0 == estates.Count)
            {
                if (TraceMe) Trace($"Discarding shop {t} because no estates.");
                continue;
            }

            var estate = estates[0];
            var buildings = estate.GetBuildings();
            if (null == buildings || 0 == buildings.Count)
            {
                if (TraceMe) Trace($"Discarding shop {t} because no building.");
                continue;
            }
            
            /*
             * Just pick the first shop front.
             */
            var shopFronts = buildings[0].GetShopFronts();
            if (null == shopFronts || shopFronts.Count == 0)
            {
                if (TraceMe) Trace($"Discarding shop {t} because no shop.");
                continue;
            }

            var myShopFront = shopFronts[0];
            var myShopFrontPoints = myShopFront.GetPoints();
            if (null == myShopFrontPoints || myShopFrontPoints.Count < 2)
            {
                if (TraceMe) Trace($"Discarding shop {t} because no shop fronts.");
                continue;
            }

            Vector3 v3Shop = (myShopFrontPoints[0] + myShopFrontPoints[1]) / 2f;
            
            /*
             * Create POI right in the middle.
             */
            var e = I.Get<engine.Engine>();
            e.QueueEntitySetupAction("poi.shop", (DefaultEcs.Entity ePOI) =>
            {
                Trace($"Generating shop at {v3Shop}");
                I.Get<TransformApi>().SetTransforms(ePOI, true, 
                    MapCameraMask, Quaternion.Identity, v3Shop with {Y=clusterDesc.AverageHeight+3f});

                DefaultEcs.Entity eMapMarker = e.CreateEntity($"poi.shop map marker");
                I.Get<HierarchyApi>().SetParent(eMapMarker, ePOI); 
                I.Get<TransformApi>().SetTransforms(eMapMarker, true, 
                    MapCameraMask, Quaternion.Identity, Vector3.Zero);
                eMapMarker.Set(new engine.world.components.MapIcon()
                    { Code = engine.world.components.MapIcon.IconCode.Game2 });
                
#if false                
                float width = 240f;
                ePOI.Set(new engine.draw.components.OSDText(
                    new Vector2(0f , -8f),
                    new Vector2(width, 18f),
                    $"shop",
                    10,
                    0xff22aaee,
                    0x00000000,
                    HAlign.Left)
                {
                    MaxDistance = 100000f
                });
#endif
    
            });
        }
    }
}