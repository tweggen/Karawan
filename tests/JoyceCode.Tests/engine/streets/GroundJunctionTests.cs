using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using engine;
using engine.streets;
using engine.streets.generation;
using engine.tale;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * WP-B6 triage — the two consumers that pick a junction out of the whole city.
 *
 * With joyce.EnableGradeSeparation on, a lifted corridor invents two junctions in the
 * air. They carry nothing but a ramp and the deck it holds up: no block corners on them
 * (WP-B5), no pavement reaches them and no pedestrian route touches them. Two consumers
 * used to draw from the junction list without knowing that -
 *
 *   - engine.Placer's random street-point branch, which is where quest destinations,
 *     taxi pickups and placed characters come from;
 *   - engine.tale.SpatialModel, which makes one street_segment location per junction and
 *     hands NPCs homes, workplaces and schedule slots at them.
 *
 * Both now go through one predicate, BlockGraph.StandsOnTheGround, which is what the
 * block trace itself already asks and is therefore not a second opinion about the same
 * thing.
 *
 * ⚠️ THE CONTROL THAT MATTERS IS THE FLAG-OFF ONE. The predicate is true of every
 * junction of every city with the flag off, so both filters are the identity there - and
 * that is asserted on the LIST, element by element and in order, because a filter that
 * quietly reorders would move which junction a given random draw lands on and every
 * fingerprint in the suite would still pass.
 */
public class GroundJunctionTests
{
    /**
     * The seeds StreetDeterminismTests pins, minus Yelukhdidru@100 which generates
     * nothing at all.
     */
    public static IEnumerable<object[]> Seeds => new List<object[]>
    {
        new object[] { "seed000", 500f },
        new object[] { "seed011", 500f },
        new object[] { "Yelukhdidru", 400f },
        new object[] { "Yelukhdidru", 800f },
        new object[] { "seed000", 1500f },
        new object[] { "seed017", 2400f },
        new object[] { "Yelukhdidru", 3000f },
    };


    /**
     * The four seeds whose flag-on city actually carries a structure, with how many
     * junctions it leaves standing on nothing. Written out so that a ruleset change which
     * quietly stops placing anything cannot leave these tests passing vacuously.
     *
     * ⚠️ Two per structure - except on Yelukhdidru@3000, where it is TWENTY-ONE for ten
     * structures. The odd one is not a deck end: it is a ramp FOOT, at level 0, whose only
     * arm is the ramp, because the one ordinary street that reached it was the corridor
     * arm the lift took away. See ARampCanComeDownAtADeadEnd.
     */
    public static IEnumerable<object[]> SeedsWithStructures => new List<object[]>
    {
        new object[] { "Yelukhdidru", 800f, 4 },
        new object[] { "seed000", 1500f, 6 },
        new object[] { "seed017", 2400f, 10 },
        new object[] { "Yelukhdidru", 3000f, 21 },
    };


    private static StrokeStore _terrain(string idString, float size, bool gradeSeparation)
    {
        if (!gradeSeparation) return StreetHarness.Generate(idString, size);

        var cluster = StreetHarness.MakeCluster(idString, size);
        var (store, _) = StreetHarness.GenerateHeavyFirstReporting(
            idString, size,
            sp => ShippedTerrain.HeightAt(
                cluster.Pos.X + sp.Pos.X, cluster.Pos.Z + sp.Pos.Y));

        return store;
    }


    /*
     * ================================================== the predicate itself =========
     */

    /**
     * Every junction of a city with the flag off stands on the ground.
     *
     * This is what makes both filters below provably the identity on the shipped city
     * that has no structures in it - and it is measured over whole generated cities
     * rather than argued from the fact that nothing emits a ramp.
     */
    [Theory]
    [MemberData(nameof(Seeds))]
    public void AFlagOffCityIsAllGround(string idString, float size)
    {
        var store = _terrain(idString, size, gradeSeparation: false);

        foreach (var sp in store.GetStreetPoints())
        {
            Assert.True(BlockGraph.StandsOnTheGround(sp),
                $"junction {sp.Id} of {idString}@{size} has no block arm, in a city that "
                + "contains no structure at all");
        }
    }


    /**
     * With the flag on, exactly the deck ends are off the ground - and there are some.
     *
     * "Off the ground" is asserted as an identity against the junctions every one of
     * whose arms is a structure member, not against a count and not against Level, so a
     * predicate that happened to refuse the right NUMBER of junctions would still fail.
     */
    [Theory]
    [MemberData(nameof(SeedsWithStructures))]
    public void OnlyADeckEndIsOffTheGround(string idString, float size, int expectedDeckEnds)
    {
        var store = _terrain(idString, size, gradeSeparation: true);

        var offTheGround = store.GetStreetPoints()
            .Where(sp => !BlockGraph.StandsOnTheGround(sp))
            .OrderBy(sp => sp.Id).ToList();

        var allArmsAreStructure = store.GetStreetPoints()
            .Where(sp => sp.GetAngleArray().Count > 0
                         && sp.GetAngleArray().All(s => StrokeKinds.IsStructure(s.Kind)))
            .OrderBy(sp => sp.Id).ToList();

        Assert.Equal(allArmsAreStructure, offTheGround);
        Assert.Equal(expectedDeckEnds, offTheGround.Count);

        /*
         * And a foot is NOT off the ground: it carries a ramp and its ordinary arms, and
         * the city stands on it exactly as before.
         */
        int feet = store.GetStreetPoints().Count(
            sp => BlockGraph.StandsOnTheGround(sp)
                  && sp.GetAngleArray().Any(s => StrokeKinds.IsStructure(s.Kind)));

        Assert.True(feet > 0, "a city with structures has ramp feet on the ground");
    }


    /**
     * ⚠️ FOUND ON THE WAY, AND NOT FIXED: a ramp may come down at a dead end.
     *
     * The placer requires the CROSSING to keep at least two arms - §11.4's interior-T-branch
     * rule - and says nothing about the far ends. So a corridor whose far end was already a
     * dead-end street gets a ramp descending into that cul-de-sac: the junction ends up
     * carrying nothing but the ramp. Nothing is disconnected by it (the ramp still reaches
     * the deck) and the road it replaces ended there too, but a ramp to nowhere is not what
     * a grade separated crossing is for.
     *
     * One such junction exists across the seven pinned seeds, and it is pinned here so that
     * a ruleset change which makes it common is visible. Refusing it is a placement policy
     * decision and would move every flag-on baseline, so WP-B6 records it instead.
     */
    [Fact]
    public void ARampCanComeDownAtADeadEnd()
    {
        var store = _terrain("Yelukhdidru", 3000f, gradeSeparation: true);

        var deadEndFeet = store.GetStreetPoints()
            .Where(sp => sp.GetAngleArray().Count == 1
                         && sp.GetAngleArray()[0].Kind == StrokeKind.Ramp)
            .ToList();

        Assert.Single(deadEndFeet);
        Assert.Equal(0, deadEndFeet[0].Level);
    }


    /*
     * ================================================== engine.Placer ================
     */

    /**
     * Flag off, the placer draws from exactly the list it drew from before.
     *
     * Same length, same order, same objects - Assert.Equal over the sequence compares
     * element by element, and StreetPoint is a reference type with no value equality, so
     * this is identity and not "a list that looks the same".
     */
    [Theory]
    [MemberData(nameof(Seeds))]
    public void ThePlacerSeesEveryJunctionOfAFlagOffCity(string idString, float size)
    {
        var store = _terrain(idString, size, gradeSeparation: false);
        var all = store.GetStreetPoints();

        Assert.Equal(all, Placer.GroundJunctionsOf(all));
    }


    /**
     * Flag on, it draws from every junction except the deck ends, in the same order.
     */
    [Theory]
    [MemberData(nameof(SeedsWithStructures))]
    public void ThePlacerNeverDrawsADeckEnd(string idString, float size, int expectedDeckEnds)
    {
        var store = _terrain(idString, size, gradeSeparation: true);
        var all = store.GetStreetPoints();
        var offered = Placer.GroundJunctionsOf(all);

        Assert.Equal(all.Count - expectedDeckEnds, offered.Count);
        Assert.Equal(all.Where(BlockGraph.StandsOnTheGround).ToList(), offered);
        Assert.DoesNotContain(offered, sp => !BlockGraph.StandsOnTheGround(sp));
    }


    /**
     * ⚠️ BOTH FILTERS PRESERVE THE ORDER THEY ARE HANDED, and the whole-city tests above
     * cannot see it.
     *
     * StrokeStore.GetStreetPoints() comes back in id order, so a filter that sorted by id
     * would be invisible to every assertion in this file - measured, that mutation passed
     * all 1677 tests. The other branch of Placer's lookup is
     * StrokeStore.QueryStreetPoints(fragment.AABB), which answers out of the octree in
     * whatever order the octree walks, and there a re-sort moves which junction a given
     * random draw lands on. So the order is asserted on a list that is deliberately NOT
     * sorted.
     */
    [Fact]
    public void NeitherFilterReordersWhatItIsGiven()
    {
        var store = _terrain("seed000", 1500f, gradeSeparation: true);

        var shuffled = store.GetStreetPoints().OrderByDescending(sp => sp.Id).ToList();
        var expected = shuffled.Where(BlockGraph.StandsOnTheGround).ToList();

        Assert.NotEqual(shuffled, shuffled.OrderBy(sp => sp.Id).ToList());
        Assert.Equal(expected, Placer.GroundJunctionsOf(shuffled));
        Assert.Equal(expected, SpatialModel.StreetLocationJunctionsOf(shuffled));
    }


    /*
     * ================================================== engine.tale.SpatialModel =====
     */

    [Theory]
    [MemberData(nameof(Seeds))]
    public void TaleMakesAStreetLocationAtEveryJunctionOfAFlagOffCity(
        string idString, float size)
    {
        var store = _terrain(idString, size, gradeSeparation: false);
        var all = store.GetStreetPoints();

        Assert.Equal(all, SpatialModel.StreetLocationJunctionsOf(all));
    }


    [Theory]
    [MemberData(nameof(SeedsWithStructures))]
    public void TaleMakesNoStreetLocationOnADeck(
        string idString, float size, int expectedDeckEnds)
    {
        var store = _terrain(idString, size, gradeSeparation: true);
        var all = store.GetStreetPoints();
        var kept = SpatialModel.StreetLocationJunctionsOf(all);

        Assert.Equal(all.Count - expectedDeckEnds, kept.Count);
        Assert.Equal(all.Where(BlockGraph.StandsOnTheGround).ToList(), kept);
    }


    /*
     * ================================================== the two call sites ===========
     */

    private static string _repoRoot()
    {
        string root = GameRoot.PathTo("JoyceCode");
        Assert.False(String.IsNullOrEmpty(root), "could not locate the checkout");

        return Path.GetFullPath(Path.Combine(root, ".."));
    }


    /**
     * The two call sites, as call EXPRESSIONS.
     *
     * Neither Placer nor SpatialModel.ExtractFrom can be constructed or driven from this
     * assembly - both reach ClusterDesc.StrokeStore(), which needs ClusterStorage, MetaGen
     * and the event queue in the I container - so the filters above are static methods
     * that a test CAN drive, and what a scan has to hold is that the call site still uses
     * them. Matching the whole assignment rather than the bare name, because a scan that
     * looks for an identifier is satisfied by a field declaration or by a comment, and one
     * that looks for a call is satisfied by `x = 0f * Call(...)`.
     */
    [Theory]
    [InlineData("JoyceCode/engine/Placer.cs",
        "listStreetPoints = GroundJunctionsOf(listStreetPoints);")]
    [InlineData("JoyceCode/engine/tale/SpatialModel.cs",
        "var streetPoints = StreetLocationJunctionsOf(strokeStore.GetStreetPoints());")]
    public void TheJunctionListIsFilteredWhereItIsRead(string relative, string expression)
    {
        string text = File.ReadAllText(Path.Combine(_repoRoot(), relative));

        Assert.Contains(expression, text);
    }
}
