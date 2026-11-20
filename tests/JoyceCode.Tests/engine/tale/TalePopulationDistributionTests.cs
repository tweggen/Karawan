using System;
using System.Collections.Generic;
using System.Linq;
using builtin.tools;
using engine.streets;
using engine.tale;
using engine.world;
using engine.world.components;
using JoyceCode.Tests.engine.streets;
using nogame.cities;
using Xunit;
using Xunit.Abstractions;

namespace JoyceCode.Tests.engine.tale;


/**
 * KAR-482: a city's NPCs live in many buildings and stand at many points, measured on the
 * shipped world ("mydear", terrain city, grade separation on, shops placed as the game does).
 *
 * Before, a building with a shop had no other use and a building had one use and one
 * standing point, so Yelukhdidru had ONE home for 279 NPCs (268 lived there) and up to
 * 156 NPCs on one exact spot; Ufyo had 9 homes for 4095 (up to 452 in one). Now buildings
 * keep their uses above their shops, office towers of 3+ storeys also house people, each
 * use holds its share of the storeys, assignment is weighted by that capacity, and a
 * building's NPCs stand at its corners and wall middles.
 */
public class TalePopulationDistributionTests
{
    private readonly ITestOutputHelper _out;
    public TalePopulationDistributionTests(ITestOutputHelper output) => _out = output;


    private sealed record City(SpatialModel Model, TalePopulationGenerator.PopulationInput Input, List<NpcSchedule> Npcs);

    private static readonly object _lo = new();
    private static readonly Dictionary<string, City> _cities = new();


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

            // As GenerateShopsOperator places them.
            var rnd = new RandomSource(cd.Name);
            ShopPlacement.Place(cd, quarters, rnd, 100f,
                v => cd.GetAttributeIntensity(v, ClusterDesc.LocationAttributes.Shopping), MapIcon.IconCode.Game2);
            ShopPlacement.Place(cd, quarters, rnd, 100f,
                v => cd.GetAttributeIntensity(v, ClusterDesc.LocationAttributes.Downtown), MapIcon.IconCode.Drink);
            ShopPlacement.Place(cd, quarters, rnd, 100f, v => 1f, MapIcon.IconCode.Eat);

            var model = SpatialModel.ExtractFrom(cd, quarters, store);
            var input = new TalePopulationGenerator.PopulationInput(cd.Index, cd.GetKey(),
                store.GetStreetPoints().Select(sp => sp.Pos3 + cd.Pos).ToList(),
                cd.GetAttributeIntensity(cd.Pos, ClusterDesc.LocationAttributes.Downtown), cd.Pos);
            var npcs = new TalePopulationGenerator().GenerateFrom(input, model);

            return _cities[name] = new City(model, input, npcs);
        }
    }


    /*
     * Measured 2026-11-20 after the change: Yelukhdidru 24 homes, at most 42 NPCs in one,
     * largest stack 8; Ufyo 329 homes, at most 100 in one, largest stack 19.
     */
    [Theory]
    [InlineData("Yelukhdidru", 279, 20, 60, 12)]
    [InlineData("Ufyo", 4095, 250, 150, 30)]
    public void NpcsLiveInManyBuildingsAndStandApart(string name, int npcs, int minHomes, int maxPerHome, int maxStack)
    {
        var c = _city(name);
        Assert.Equal(npcs, c.Npcs.Count);

        int homes = c.Model.Locations.Count(l => l.Type == "home");
        int perHome = c.Npcs.GroupBy(n => n.HomeLocationId).Max(g => g.Count());
        int stack = c.Npcs.GroupBy(n => n.CurrentWorldPosition).Max(g => g.Count());
        _out.WriteLine($"{name}: {homes} homes, at most {perHome} NPCs in one, largest stack {stack}");

        Assert.True(homes >= minHomes, $"{homes} homes");
        Assert.True(perHome <= maxPerHome, $"{perHome} NPCs share one home");
        Assert.True(stack <= maxStack, $"{stack} NPCs stand on one exact spot");
    }


    [Theory]
    [InlineData("Yelukhdidru")]
    [InlineData("Ufyo")]
    public void AssignmentFollowsCapacity(string name)
    {
        var c = _city(name);
        var homes = c.Model.Locations.Where(l => l.Type == "home").ToList();
        Assert.All(homes, h => Assert.True(h.Capacity >= 1));

        // The largest homes take more residents than the smallest, on average.
        var byCapacity = homes.OrderBy(h => h.Capacity).ToList();
        int third = Math.Max(1, byCapacity.Count / 3);
        double Residents(IEnumerable<Location> ls) => ls.Average(l => c.Npcs.Count(n => n.HomeLocationId == l.Id));
        Assert.True(Residents(byCapacity.TakeLast(third)) > Residents(byCapacity.Take(third)));
    }


    [Fact]
    public void ABuildingsNpcsStandAtItsCornersAndWalls()
    {
        var c = _city("Yelukhdidru");
        foreach (var l in c.Model.Locations.Where(l => l.Type is "home" or "office" or "warehouse"))
        {
            Assert.NotNull(l.EntryCandidates);
            Assert.True(l.EntryCandidates.Count >= 6, $"{l.Type} {l.Id} has {l.EntryCandidates.Count} standing points");
            Assert.Equal(l.EntryCandidates[0], l.EntryPosition);
        }
    }


    /**
     * The seed hierarchy's promise: each NPC is generated from its own seed, so skipping
     * one (the skip mask of a cluster's player-known NPCs) changes nobody else. A
     * capacity-weighted pick must keep it - weighting, not counting who is already there.
     */
    [Fact]
    public void SkippingAnNpcChangesNobodyElse()
    {
        var c = _city("Yelukhdidru");
        var skip = new HashSet<int> { 0, 5, 17, 100, 278 };
        var without = new TalePopulationGenerator().GenerateFrom(c.Input, c.Model, skip);

        var all = c.Npcs.ToDictionary(n => n.NpcId);
        Assert.Equal(c.Npcs.Count - skip.Count, without.Count);
        foreach (var n in without)
        {
            var o = all[n.NpcId];
            Assert.Equal(o.Role, n.Role);
            Assert.Equal(o.HomeLocationId, n.HomeLocationId);
            Assert.Equal(o.WorkplaceLocationId, n.WorkplaceLocationId);
            Assert.Equal(o.SocialVenueIds, n.SocialVenueIds);
            Assert.Equal(o.CurrentWorldPosition, n.CurrentWorldPosition);
        }
    }
}
