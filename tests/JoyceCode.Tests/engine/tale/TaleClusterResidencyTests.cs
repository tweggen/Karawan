using System;
using engine.joyce;
using engine.tale;
using Xunit;

namespace JoyceCode.Tests.engine.tale;

/// <summary>
/// TALE S0 W4 (KAR-434): a cluster is due for depopulation once no loaded
/// fragment has touched it for the grace period.
/// </summary>
public class TaleClusterResidencyTests
{
    private static readonly DateTime T0 = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(60);
    private static readonly Index3 F1 = new(1, 0, 1);
    private static readonly Index3 F2 = new(2, 0, 1);


    [Fact]
    public void NotDueBeforeGrace_DueAfterLastPurge()
    {
        var r = new TaleClusterResidency();
        r.FragmentActive(5, F1);
        r.FragmentActive(5, F2);

        r.FragmentPurged(F1, T0);
        Assert.Equal(1, r.ResidentCount(5));
        Assert.Empty(r.DueForDepopulate(new[] { 5 }, T0.AddSeconds(120), Grace));

        r.FragmentPurged(F2, T0.AddSeconds(10));
        Assert.Empty(r.DueForDepopulate(new[] { 5 }, T0.AddSeconds(69), Grace));
        Assert.Equal(new[] { 5 }, r.DueForDepopulate(new[] { 5 }, T0.AddSeconds(70), Grace));
    }


    [Fact]
    public void ReactivationCancelsPendingDepopulate()
    {
        var r = new TaleClusterResidency();
        r.FragmentActive(5, F1);
        r.FragmentPurged(F1, T0);
        r.FragmentActive(5, F1);

        Assert.Empty(r.DueForDepopulate(new[] { 5 }, T0.AddSeconds(600), Grace));
    }


    [Fact]
    public void BorderFragment_CountsForEveryIntersectingCluster()
    {
        var r = new TaleClusterResidency();
        r.FragmentActive(5, F1);   // border fragment of 5 and 6
        r.FragmentActive(6, F1);
        r.FragmentActive(5, F2);   // 5 has another one

        r.FragmentPurged(F1, T0);

        Assert.Equal(new[] { 6 }, r.DueForDepopulate(new[] { 5, 6 }, T0.AddSeconds(60), Grace));
    }


    [Fact]
    public void PopulatedButNeverResident_StartsGraceAtFirstCheck()
    {
        var r = new TaleClusterResidency();

        Assert.Empty(r.DueForDepopulate(new[] { 7 }, T0, Grace));
        Assert.Empty(r.DueForDepopulate(new[] { 7 }, T0.AddSeconds(59), Grace));
        Assert.Equal(new[] { 7 }, r.DueForDepopulate(new[] { 7 }, T0.AddSeconds(60), Grace));
    }


    [Fact]
    public void Depopulated_ForgetsTheGracePeriod()
    {
        var r = new TaleClusterResidency();
        r.FragmentActive(5, F1);
        r.FragmentPurged(F1, T0);
        Assert.Equal(new[] { 5 }, r.DueForDepopulate(new[] { 5 }, T0.AddSeconds(60), Grace));

        r.Depopulated(5);

        // Populated again later without a resident fragment: a fresh grace period.
        Assert.Empty(r.DueForDepopulate(new[] { 5 }, T0.AddSeconds(100), Grace));
        Assert.Equal(new[] { 5 }, r.DueForDepopulate(new[] { 5 }, T0.AddSeconds(160), Grace));
    }
}
