using System.Collections.Generic;
using System.Linq;
using engine.streets;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * Ids must be unique within a street network.
 *
 * They were not. An Id packs the cluster into its high 16 bits and a sequence number
 * into its low 16, and that sequence came from a process-global counter: after 65535
 * points anywhere in the process it wrapped, and two points in the same network could
 * end up sharing an Id. That silently corrupts the adjacency set behind AreConnected,
 * makes GetStreetPoint return the wrong point, and collides on the LiteDB primary key.
 *
 * It stayed hidden because a collision only bites when the wrap lands inside one
 * generation's window, and because nothing ever checked. This suite alone gets through
 * the whole 16-bit budget about fifty times over.
 */
public class StreetPointIdTests
{
    [Theory]
    [MemberData(nameof(StreetDeterminismTests.Seeds), MemberType = typeof(StreetDeterminismTests))]
    public void IdsAreUniqueWithinAGeneratedNetwork(string idString, float size)
    {
        var store = StreetHarness.Generate(idString, size);

        var pointIds = store.GetStreetPoints().Select(sp => sp.Id).ToList();
        Assert.Equal(pointIds.Count, pointIds.Distinct().Count());

        var strokeIds = store.GetStrokes().Select(s => s.Sid).ToList();
        Assert.Equal(strokeIds.Count, strokeIds.Distinct().Count());
    }


    /**
     * The load-bearing property: a network's ids depend on that network alone, not on
     * how much the process happened to allocate beforehand. Under the old global
     * counter these two sets were disjoint and drifted further apart with every test
     * that ran first.
     */
    [Fact]
    public void IdsDoNotDependOnWhatWasGeneratedBefore()
    {
        var first = StreetHarness.Generate("Yelukhdidru", 800f);
        var second = StreetHarness.Generate("Yelukhdidru", 800f);

        Assert.Equal(
            first.GetStreetPoints().Select(sp => sp.Id).OrderBy(x => x).ToList(),
            second.GetStreetPoints().Select(sp => sp.Id).OrderBy(x => x).ToList());

        Assert.Equal(
            first.GetStrokes().Select(s => s.Sid).OrderBy(x => x).ToList(),
            second.GetStrokes().Select(s => s.Sid).OrderBy(x => x).ToList());
    }


    /**
     * The sequence has to fit in the low 16 bits of the Id, so a network cannot hold
     * more than 65535 of either. Generating far more than the old global budget must
     * therefore stay comfortably inside each network's own.
     */
    [Fact]
    public void ManyNetworksInOneProcessNeverExhaustTheSequence()
    {
        int totalPoints = 0;

        /*
         * The largest seed yields ~1380 points, so this needs about fifty passes to
         * clear the old 65535 global budget. Costs a few seconds and is worth it: it is
         * the only test that actually crosses the boundary the bug lived at.
         */
        for (int i = 0; i < 50; ++i)
        {
            var store = StreetHarness.Generate("Yelukhdidru", 3000f);
            totalPoints += store.GetStreetPoints().Count;

            foreach (var sp in store.GetStreetPoints())
            {
                Assert.InRange(sp.Id & 0xffff, 1, 0xffff);
            }
        }

        Assert.True(totalPoints > 0xffff,
            $"expected to allocate past the old 16-bit global budget, only reached {totalPoints}");
    }


    /**
     * A point that is not in any network still needs an identity: section maps key on
     * it, and so does anything building a junction by hand.
     */
    [Fact]
    public void PointsOutsideAnyNetworkStillHaveDistinctIds()
    {
        var ids = new HashSet<int>();
        for (int i = 0; i < 100; ++i)
        {
            Assert.True(ids.Add(new StreetPoint().Id), "provisional ids must be distinct");
        }
    }
}
