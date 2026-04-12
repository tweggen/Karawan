using System.Collections.Generic;
using System.IO;
using engine;
using engine.tale.bake;
using Xunit;

namespace JoyceCode.Tests.engine.tale.bake;

/// <summary>
/// Tests for ScenarioSelector.Pick. The selector reads its bake request list
/// via engine.Assets.GetAssetImplementation(), so each test installs a fresh
/// <see cref="FakeAssetImplementation"/> with a curated AvailableScenarios
/// list before exercising the picker. xUnit runs tests in the same class
/// serially by default, so the global static doesn't race here.
/// </summary>
public class ScenarioSelectorTests
{
    /// <summary>
    /// Minimal AAssetImplementation override for tests. No real asset I/O —
    /// only the AvailableScenarios list (inherited from the base class) is
    /// load-bearing for the selector.
    /// </summary>
    private class FakeAssetImplementation : AAssetImplementation
    {
        public override Stream Open(in string filename) => null;
        public override bool Exists(in string filename) => false;
        public override void AddAssociation(string tag, string uri) { }
        public override IReadOnlyDictionary<string, string> GetAssets()
            => new Dictionary<string, string>();
    }

    private static FakeAssetImplementation MakeImpl(params (string cat, int idx, int npcCount, int seed)[] requests)
    {
        var impl = new FakeAssetImplementation();
        // Constructor auto-registers via engine.Assets.SetAssetImplementation,
        // so the selector will pick this up immediately.
        foreach (var (cat, idx, npcCount, seed) in requests)
        {
            impl.AvailableScenarios.Add(new AAssetImplementation.ScenarioBakeRequest
            {
                CategoryName = cat,
                Index = idx,
                NpcCount = npcCount,
                Seed = seed,
                SimulationDays = 365
            });
        }
        return impl;
    }

    [Fact]
    public void Pick_NoRequests_ReturnsNull()
    {
        MakeImpl(); // empty
        var picked = new ScenarioSelector().Pick(targetNpcCount: 100, clusterSeed: 0);
        Assert.Null(picked);
    }

    [Fact]
    public void Pick_DeterministicForSameInputs()
    {
        MakeImpl(
            ("small",  0, 50,  10000),
            ("small",  1, 60,  10001),
            ("medium", 0, 200, 20000),
            ("medium", 1, 220, 20001));

        var selector = new ScenarioSelector();
        var a = selector.Pick(targetNpcCount: 200, clusterSeed: 7);
        var b = selector.Pick(targetNpcCount: 200, clusterSeed: 7);
        Assert.Equal(a.CategoryName, b.CategoryName);
        Assert.Equal(a.Index, b.Index);
    }

    [Fact]
    public void Pick_PrefersClosestMedianCategory_Small()
    {
        // small medians around 55, medium around 210. Target 60 is closer
        // to small than medium.
        MakeImpl(
            ("small",  0, 50,  10000),
            ("small",  1, 60,  10001),
            ("small",  2, 70,  10002),
            ("medium", 0, 200, 20000),
            ("medium", 1, 220, 20001));

        var picked = new ScenarioSelector().Pick(targetNpcCount: 60, clusterSeed: 0);
        Assert.Equal("small", picked.CategoryName);
    }

    [Fact]
    public void Pick_PrefersClosestMedianCategory_Medium()
    {
        MakeImpl(
            ("small",  0, 50,  10000),
            ("small",  1, 60,  10001),
            ("medium", 0, 200, 20000),
            ("medium", 1, 220, 20001),
            ("medium", 2, 240, 20002));

        var picked = new ScenarioSelector().Pick(targetNpcCount: 220, clusterSeed: 0);
        Assert.Equal("medium", picked.CategoryName);
    }

    [Fact]
    public void Pick_RoundRobinsWithinCategoryByClusterSeed()
    {
        // Three small scenarios, target solidly in the small range. Different
        // cluster seeds should land on different indices, and the mapping
        // must be (clusterSeed mod count).
        MakeImpl(
            ("small", 0, 50, 10000),
            ("small", 1, 60, 10001),
            ("small", 2, 70, 10002));

        var selector = new ScenarioSelector();
        Assert.Equal(0, selector.Pick(60, 0).Index);
        Assert.Equal(1, selector.Pick(60, 1).Index);
        Assert.Equal(2, selector.Pick(60, 2).Index);
        Assert.Equal(0, selector.Pick(60, 3).Index);
    }

    [Fact]
    public void Pick_NegativeClusterSeed_StillReturnsValidIndex()
    {
        MakeImpl(
            ("small", 0, 50, 10000),
            ("small", 1, 60, 10001),
            ("small", 2, 70, 10002));

        var picked = new ScenarioSelector().Pick(targetNpcCount: 60, clusterSeed: -7);
        // (-7 mod 3 + 3) mod 3 = 2, so index should be 2.
        Assert.Equal(2, picked.Index);
    }
}
