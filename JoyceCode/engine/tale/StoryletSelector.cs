using System;
using System.Collections.Generic;

namespace engine.tale;

public enum StoryletLocationType
{
    Home,
    Workplace,
    SocialVenue,
    EatVenue,
    Street
}

public class StoryletTemplate
{
    public string Id;
    public float DurationMinutes;
    public StoryletLocationType LocationType;
}

/// <summary>
/// Placeholder storylet selector using hardcoded schedule templates per role.
/// Phase 1 replaces this with the full storylet library.
/// </summary>
public class StoryletSelector
{
    private static readonly StoryletTemplate[] WorkerSchedule =
    {
        new() { Id = "wake_up",      DurationMinutes = 45,  LocationType = StoryletLocationType.Home },
        new() { Id = "work_manual",  DurationMinutes = 270, LocationType = StoryletLocationType.Workplace },
        new() { Id = "lunch_break",  DurationMinutes = 30,  LocationType = StoryletLocationType.EatVenue },
        new() { Id = "work_manual",  DurationMinutes = 270, LocationType = StoryletLocationType.Workplace },
        new() { Id = "socialize",    DurationMinutes = 150, LocationType = StoryletLocationType.SocialVenue },
        new() { Id = "sleep",        DurationMinutes = 570, LocationType = StoryletLocationType.Home },
    };

    private static readonly StoryletTemplate[] MerchantSchedule =
    {
        new() { Id = "wake_up",          DurationMinutes = 45,  LocationType = StoryletLocationType.Home },
        new() { Id = "open_shop",        DurationMinutes = 240, LocationType = StoryletLocationType.Workplace },
        new() { Id = "lunch_break",      DurationMinutes = 30,  LocationType = StoryletLocationType.EatVenue },
        new() { Id = "serve_customers",  DurationMinutes = 240, LocationType = StoryletLocationType.Workplace },
        new() { Id = "socialize",        DurationMinutes = 150, LocationType = StoryletLocationType.SocialVenue },
        new() { Id = "sleep",            DurationMinutes = 630, LocationType = StoryletLocationType.Home },
    };

    private static readonly StoryletTemplate[] SocialiteSchedule =
    {
        new() { Id = "wake_up_late", DurationMinutes = 60,  LocationType = StoryletLocationType.Home },
        new() { Id = "wander",       DurationMinutes = 120, LocationType = StoryletLocationType.SocialVenue },
        new() { Id = "eat_out",      DurationMinutes = 60,  LocationType = StoryletLocationType.EatVenue },
        new() { Id = "socialize",    DurationMinutes = 240, LocationType = StoryletLocationType.SocialVenue },
        new() { Id = "bar",          DurationMinutes = 180, LocationType = StoryletLocationType.SocialVenue },
        new() { Id = "sleep_late",   DurationMinutes = 660, LocationType = StoryletLocationType.Home },
    };

    private static readonly StoryletTemplate[] DrifterSchedule =
    {
        new() { Id = "wake_anywhere",  DurationMinutes = 30,  LocationType = StoryletLocationType.Home },
        new() { Id = "scavenge",       DurationMinutes = 180, LocationType = StoryletLocationType.Street },
        new() { Id = "wander",         DurationMinutes = 300, LocationType = StoryletLocationType.Street },
        new() { Id = "rest",           DurationMinutes = 120, LocationType = StoryletLocationType.Home },
        new() { Id = "sleep_anywhere", DurationMinutes = 600, LocationType = StoryletLocationType.Home },
    };

    private static readonly Dictionary<string, StoryletTemplate[]> Schedules = new()
    {
        { "Worker", WorkerSchedule },
        { "Merchant", MerchantSchedule },
        { "Socialite", SocialiteSchedule },
        { "Drifter", DrifterSchedule },
    };


    public StoryletTemplate[] GetScheduleForRole(string role)
    {
        return Schedules.TryGetValue(role, out var schedule) ? schedule : WorkerSchedule;
    }


    public StoryletTemplate SelectNext(NpcSchedule npc)
    {
        var schedule = GetScheduleForRole(npc.Role);
        return schedule[npc.ScheduleStep % schedule.Length];
    }


    public void AdvanceStep(NpcSchedule npc)
    {
        var schedule = GetScheduleForRole(npc.Role);
        npc.ScheduleStep = (npc.ScheduleStep + 1) % schedule.Length;
    }


    /// <summary>
    /// Apply postconditions from a completed storylet.
    /// Returns a dictionary of property deltas for logging.
    /// </summary>
    public Dictionary<string, float> ApplyPostconditions(NpcSchedule npc, string storyletId,
        float durationMinutes, Dictionary<string, float> deltasBuffer)
    {
        deltasBuffer.Clear();

        switch (storyletId)
        {
            case "work_manual":
            case "open_shop":
            case "serve_customers":
                RecordDelta(npc, "fatigue", 0.28f, deltasBuffer);
                RecordDelta(npc, "wealth", 0.08f, deltasBuffer);
                RecordHungerTick(npc, durationMinutes, deltasBuffer);
                break;

            case "sleep":
            case "sleep_late":
            case "sleep_anywhere":
                RecordSet(npc, "fatigue", 0.1f, deltasBuffer);
                break;

            case "lunch_break":
            case "eat_out":
                RecordDelta(npc, "hunger", -0.55f, deltasBuffer);
                RecordDelta(npc, "wealth", -0.03f, deltasBuffer);
                RecordHungerTick(npc, durationMinutes, deltasBuffer);
                break;

            default:
                RecordHungerTick(npc, durationMinutes, deltasBuffer);
                break;
        }

        return deltasBuffer;
    }


    private static void RecordDelta(NpcSchedule npc, string prop, float delta,
        Dictionary<string, float> deltas)
    {
        float old = npc.Properties.GetValueOrDefault(prop, 0.5f);
        float val = Math.Clamp(old + delta, 0f, 1f);
        npc.Properties[prop] = val;
        float actualDelta = val - old;
        if (deltas.ContainsKey(prop))
            deltas[prop] += actualDelta;
        else
            deltas[prop] = actualDelta;
    }


    private static void RecordSet(NpcSchedule npc, string prop, float value,
        Dictionary<string, float> deltas)
    {
        float old = npc.Properties.GetValueOrDefault(prop, 0.5f);
        npc.Properties[prop] = value;
        deltas[prop] = value - old;
    }


    private static void RecordHungerTick(NpcSchedule npc, float durationMinutes,
        Dictionary<string, float> deltas)
    {
        float hoursAwake = durationMinutes / 60f;
        float delta = 0.06f * hoursAwake;
        RecordDelta(npc, "hunger", delta, deltas);
    }
}
