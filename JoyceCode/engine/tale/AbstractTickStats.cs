using System.Diagnostics;

namespace engine.tale;

/// <summary>
/// What the background tick for Tier-3 schedules did (S0 W5), summed over
/// one or more TaleManager.AdvanceAbstractSchedules / CatchUpCluster calls.
/// </summary>
public class AbstractTickStats
{
    /// <summary>Schedules looked at.</summary>
    public int Scanned;

    /// <summary>Schedules skipped because an entity runs them (Tier 1/2).</summary>
    public int SkippedMaterialised;

    /// <summary>Schedules advanced by at least one step.</summary>
    public int Advanced;

    /// <summary>AdvanceNpc steps taken, at event time.</summary>
    public int Steps;

    /// <summary>Schedules more than a day behind, resynced with one step at now.</summary>
    public int Resynced;

    /// <summary>Schedules that hit the step cap and were resynced at now.</summary>
    public int Capped;

    /// <summary>Schedules whose storylet selection failed; retried later.</summary>
    public int BackedOff;

    /// <summary>Stopwatch ticks spent.</summary>
    public long ElapsedTicks;

    public double ElapsedMs => ElapsedTicks * 1000.0 / Stopwatch.Frequency;


    public void Add(AbstractTickStats other)
    {
        Scanned += other.Scanned;
        SkippedMaterialised += other.SkippedMaterialised;
        Advanced += other.Advanced;
        Steps += other.Steps;
        Resynced += other.Resynced;
        Capped += other.Capped;
        BackedOff += other.BackedOff;
        ElapsedTicks += other.ElapsedTicks;
    }


    public void Reset()
    {
        Scanned = SkippedMaterialised = Advanced = Steps = Resynced = Capped = BackedOff = 0;
        ElapsedTicks = 0;
    }


    public override string ToString() =>
        $"scanned={Scanned} materialised={SkippedMaterialised} advanced={Advanced} steps={Steps} " +
        $"resynced={Resynced} capped={Capped} backed_off={BackedOff} time={ElapsedMs:F2} ms";
}
