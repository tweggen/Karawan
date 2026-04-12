using engine.tale.bake;
using Xunit;

namespace JoyceCode.Tests.engine.tale.bake;

/// <summary>
/// Determinism + format checks for ScenarioFileName.Of, the SHA256+base64
/// hash that maps a scenario identity to its bake artifact filename. This is
/// the contract that lets Chushi (build) and the runtime ScenarioLibrary agree
/// on which file goes where, so any drift in the hash function silently
/// breaks the entire D1+D2 pipeline. The matching helper in
/// Tooling/Cmdline/GameConfig.cs is intentionally duplicated and must produce
/// the same output (verified manually by build pipeline cross-checking
/// AndroidResources.xml entries against generated/sc-* files).
/// </summary>
public class ScenarioFileNameTests
{
    [Fact]
    public void Of_SameInputs_ReturnsSameHash()
    {
        var a = ScenarioFileName.Of("medium", 3, 20003);
        var b = ScenarioFileName.Of("medium", 3, 20003);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Of_DifferentSeed_ReturnsDifferentHash()
    {
        var a = ScenarioFileName.Of("medium", 3, 20003);
        var b = ScenarioFileName.Of("medium", 3, 20004);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Of_DifferentIndex_ReturnsDifferentHash()
    {
        var a = ScenarioFileName.Of("medium", 3, 20000);
        var b = ScenarioFileName.Of("medium", 4, 20000);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Of_DifferentCategory_ReturnsDifferentHash()
    {
        var a = ScenarioFileName.Of("medium", 0, 0);
        var b = ScenarioFileName.Of("large", 0, 0);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Of_HasScPrefix()
    {
        var name = ScenarioFileName.Of("small", 0, 10000);
        Assert.StartsWith("sc-", name);
    }

    [Fact]
    public void Of_UsesUrlSafeBase64()
    {
        // The base64 alphabet is mapped to URL-safe form: '+' -> '-',
        // '/' -> '_', '=' -> '~'. None of the original chars should leak.
        var name = ScenarioFileName.Of("medium", 7, 99999);
        Assert.DoesNotContain("+", name);
        Assert.DoesNotContain("/", name);
        Assert.DoesNotContain("=", name);
    }

    [Fact]
    public void BuildKey_FormatIsStable()
    {
        // Lock the key format so any reformatting (e.g. adding a separator
        // or changing field order) shows up here loudly. The hash output
        // depends on this exact string.
        Assert.Equal("medium;3;20003", ScenarioFileName.BuildKey("medium", 3, 20003));
    }

    [Fact]
    public void OfKey_AndOf_ProduceSameResult()
    {
        var direct = ScenarioFileName.Of("large", 11, 30011);
        var viaKey = ScenarioFileName.OfKey(ScenarioFileName.BuildKey("large", 11, 30011));
        Assert.Equal(direct, viaKey);
    }
}
