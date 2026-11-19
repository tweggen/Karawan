using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using builtin.tools;
using engine.streets;
using engine.world;
using engine.world.components;
using JoyceCode.Tests.engine.streets;
using nogame.cities;
using Xunit;
using Xunit.Abstractions;

namespace JoyceCode.Tests.nogame;


/**
 * KAR-479: where a cluster's shops go, measured on the shipped world (seed "mydear",
 * terrain-following, grade separation on), with GenerateShopsOperator's own seed, zone
 * functions and order.
 *
 * Found from a playtest: TALE reported Yelukhdidru as "28 buildings, 1 shops". Both
 * were true. The one shop was the bug: ShopPlacement accepted a shopfront only within
 * 5 m of the kerb line the pedestrian lane runs on, but a building stands its block's
 * pavement width back from that line, and downtown pavements are 6 m - so every
 * kerb-facing front of every downtown block was refused. Measured before the fix:
 * Yelukhdidru 0 Game2, 1 Drink, 0 Eat; Ufyo 0 / 47 / 84.
 */
public class ShopPlacementTests
{
    private readonly ITestOutputHelper _out;
    public ShopPlacementTests(ITestOutputHelper output) => _out = output;


    private sealed record City(ClusterDesc Cluster, QuarterStore Quarters,
        List<ShopPlacement.PlacedShop> Game2, List<ShopPlacement.PlacedShop> Drink, List<ShopPlacement.PlacedShop> Eat)
    {
        public IEnumerable<ShopPlacement.PlacedShop> All => Game2.Concat(Drink).Concat(Eat);
    }


    private static readonly object _lo = new();
    private static readonly Dictionary<string, City> _cities = new();


    /*
     * One city, placed the way GenerateShopsOperator.ClusterOperatorApply places it.
     */
    private static City _city(string name)
    {
        lock (_lo)
        {
            if (_cities.TryGetValue(name, out var c)) return c;

            new GenerateClustersOperator("mydear")._generateClusterList(null, out var clusters);
            var cd = clusters.Single(x => x.Name == name);
            var (store, _) = StreetHarness.GenerateHeavyFirstReporting(cd.IdString, cd.Size,
                sp => ShippedTerrain.HeightAt(cd.Pos.X + sp.Pos.X, cd.Pos.Z + sp.Pos.Y));
            cd.StreetHeightSource = ShippedTerrain.StreetHeightsOf(cd, store);
            var quarters = StreetHarness.GenerateQuarters(cd, store, cd.IdString);

            var rnd = new RandomSource(cd.Name);
            var game2 = ShopPlacement.Place(cd, quarters, rnd, 100f,
                v => cd.GetAttributeIntensity(v, ClusterDesc.LocationAttributes.Shopping), MapIcon.IconCode.Game2);
            var drink = ShopPlacement.Place(cd, quarters, rnd, 100f,
                v => cd.GetAttributeIntensity(v, ClusterDesc.LocationAttributes.Downtown), MapIcon.IconCode.Drink);
            var eat = ShopPlacement.Place(cd, quarters, rnd, 100f, v => 1f, MapIcon.IconCode.Eat);

            return _cities[name] = new City(cd, quarters, game2, drink, eat);
        }
    }


    public static IEnumerable<object[]> Cities() =>
        new[] { "Yelukhdidru", "Ufyo" }.Select(n => new object[] { n });


    [Theory]
    [MemberData(nameof(Cities))]
    public void ACityHasShopsOfEveryKind(string name)
    {
        var c = _city(name);
        _out.WriteLine($"{name}: {c.Game2.Count} Game2, {c.Drink.Count} Drink, {c.Eat.Count} Eat");

        // Measured 2026-11-19 after the fix: Yelukhdidru 8 / 21 / 20, Ufyo 59 / 175 / 214.
        int perKind = name == "Yelukhdidru" ? 5 : 40;
        Assert.True(c.Game2.Count >= perKind, $"{c.Game2.Count} Game2");
        Assert.True(c.Drink.Count >= perKind, $"{c.Drink.Count} Drink");
        Assert.True(c.Eat.Count >= perKind, $"{c.Eat.Count} Eat");
    }


    [Theory]
    [MemberData(nameof(Cities))]
    public void EveryShopfrontHoldsAtMostOneShop(string name)
    {
        var c = _city(name);
        var fronts = c.Quarters.GetQuarters().SelectMany(q => q.GetEstates()).SelectMany(e => e.GetBuildings())
            .SelectMany(b => b.GetShopFronts()).ToList();

        Assert.Equal(c.All.Count(), fronts.Count(ShopPlacement.IsShop));
        foreach (var f in fronts)
        {
            int kinds = new[] { MapIcon.IconCode.Game2, MapIcon.IconCode.Drink, MapIcon.IconCode.Eat }
                .Count(k => f.Tags.Contains($"shop {k}"));
            Assert.True(kinds <= 1, $"a shopfront carries {kinds} shops");
        }
    }


    [Theory]
    [MemberData(nameof(Cities))]
    public void EveryShopFacesItsBlocksPavement_AndWidePavementsGetShops(string name)
    {
        var c = _city(name);
        foreach (var s in c.All)
            Assert.True(ShopPlacement.HasPedestrianAccess(s.Local, s.Quarter));

        // The defect: no shop at all on a block with a 6 m pavement.
        Assert.Contains(c.All, s => s.Quarter.SidewalkWidth >= 6f);
    }


    [Fact]
    public void AKerbFacingFrontOnASixMetrePavementHasAccess_AnInteriorOneDoesNot()
    {
        var c = _city("Yelukhdidru");
        var wide = c.Quarters.GetQuarters().First(q => q.SidewalkWidth >= 6f && q.GetDelims().Count >= 3);
        var delims = wide.GetDelims();

        // Midway along the first kerb, 6.1 m inwards - where the wall's front is.
        Vector2 a = delims[0].StartPoint, b = delims[1].StartPoint;
        Vector2 mid = (a + b) / 2f, dir = Vector2.Normalize(b - a);
        Vector2 centre = wide.GetCenterPoint();
        Vector2 inward = new(-dir.Y, dir.X);
        if (Vector2.Dot(centre - mid, inward) < 0) inward = -inward;

        Vector2 front = mid + inward * 6.1f;
        Assert.True(ShopPlacement.HasPedestrianAccess(new Vector3(front.X, 0f, front.Y), wide));

        Vector2 deep = mid + inward * 20f;
        float kerbDistance = delims.Select((d, i) => _dist(deep, d.StartPoint, delims[(i + 1) % delims.Count].StartPoint)).Min();
        Assert.True(kerbDistance > wide.SidewalkWidth + 1f, $"the deep point is only {kerbDistance:F1} m from a kerb");
        Assert.False(ShopPlacement.HasPedestrianAccess(new Vector3(deep.X, 0f, deep.Y), wide));
    }


    private static float _dist(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float t = Math.Clamp(Vector2.Dot(p - a, ab) / MathF.Max(1e-6f, ab.LengthSquared()), 0f, 1f);
        return Vector2.Distance(p, a + t * ab);
    }
}
