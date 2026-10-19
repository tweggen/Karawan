using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using engine.tale;
using engine.tale.bake;
using JoyceCode.Tests.engine.tale.bake;
using Xunit;

namespace JoyceCode.Tests.engine.tale;

/// <summary>
/// The shared social metrics on a real DES run (TALE S0 W6, KAR-448): the
/// Testbed's "social" block is the calculator's output on the same data, its
/// trust density is the bake report's RelationshipDensity, and the rates come
/// from counters rather than literals.
/// </summary>
public class DesSocialMetricsTests : IClassFixture<ScenarioCompilerTests.RegistryFixture>
{
    private const int Days = 35;

    /**
     * One run for all tests, started lazily: the fixture must have registered
     * the registries DesSimulation reaches for before it starts.
     */
    private static readonly System.Lazy<DesSimulation> _sim = new(() => new ScenarioCompiler
    {
        CategoryName = "metrics-test",
        Index = 0,
        NpcCount = 40,
        Seed = 4711,
        SimulationDays = Days,
        OutputDirectory = ""
    }.RunSimulation(Days));

    private static DesSimulation Sim => _sim.Value;

    public DesSocialMetricsTests(ScenarioCompilerTests.RegistryFixture _) { }


    private static Dictionary<string, object> FinalMetrics() =>
        Sim.Metrics.ComputeFinalMetrics(
            Sim.Npcs, Sim.Relationships, Days, Sim.Encounters.TotalEncounters,
            Sim.Encounters, new SpatialModel(), Sim.ComputeSocialMetrics());


    [Fact]
    public void TheTestbedSocialBlockIsTheCalculator()
    {
        var social = FinalMetrics()["social"];
        string fromTestbed = JsonSerializer.Serialize(social);
        string fromCalculator = JsonSerializer.Serialize(Sim.ComputeSocialMetrics().ToDictionary());
        Assert.Equal(fromCalculator, fromTestbed);
    }


    [Fact]
    public void TrustDensityIsTheBakeReportsRelationshipDensity()
    {
        var scenario = ScenarioExporter.Build("metrics-test", 0, 4711, Days, Sim);
        var stats = ScenarioStatisticsBuilder.From(scenario);
        var m = Sim.ComputeSocialMetrics();

        Assert.Equal(stats.RelationshipCount, m.KnownPairs);
        Assert.Equal(stats.RelationshipDensity, m.TrustDensity, 9);
        Assert.True(m.KnownPairs > 0, "a 35-day run with 40 NPCs should record some trust");
    }


    [Fact]
    public void MembershipComesFromTheSchedules()
    {
        var m = Sim.ComputeSocialMetrics();
        int inGroups = Sim.Npcs.Values.Count(n => n.GroupId >= 0);
        Assert.Equal(inGroups, m.NpcsInGroups);
        Assert.Equal(Sim.Npcs.Values.Where(n => n.GroupId >= 0).Select(n => n.GroupId).Distinct().Count(),
            m.GroupCount);
        Assert.Equal(Days - 1, m.HistoryDays);    // one sample per completed day boundary
    }


    [Fact]
    public void RatesComeFromCounters()
    {
        var d = FinalMetrics();
        int selections = Sim.Metrics.SelectionsBySource.Values.Sum();
        Assert.True(selections > 0);

        double own = Sim.Metrics.SelectionsBySource
            .Where(kv => MetricsCollector.IsOwnRoutine(kv.Key)).Sum(kv => kv.Value);
        Assert.Equal(System.Math.Round(own / selections, 3), d["routine_completion_rate"]);

        var bySource = Assert.IsType<Dictionary<string, object>>(d["selections_by_source"]);
        Assert.Equal(selections, bySource.Values.Sum(v => (int)v));

        object fulfilment = d["request_fulfillment_rate"];
        if (Sim.Metrics.RequestsEmitted == 0)
            Assert.Null(fulfilment);
        else
            Assert.Equal(System.Math.Round((double)Sim.Metrics.RequestsFulfilled / Sim.Metrics.RequestsEmitted, 3),
                fulfilment);
    }
}
