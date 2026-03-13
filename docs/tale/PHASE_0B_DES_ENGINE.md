# Phase 0B — Discrete Event Simulation Engine

**Prerequisites**: Phase 0A (spatial model exists, NPCs assigned to locations).
**Read also**: `REFERENCE.md` for spatial verbs, base properties, simulation tiers.

---

## Goal

Implement the Tier 3 discrete event simulation. NPCs advance from story node to story node by jumping between events. Encounters computed probabilistically from spatial geometry. No per-frame ticking. A simulated year for 500 NPCs should run in seconds.

This code is **production code** — it runs in the real game for background (Tier 3) NPCs. It lives in JoyceCode or nogameCode, not in the Testbed project.

## What To Build

### 1. Event Queue

A min-heap priority queue ordered by game time:

```csharp
public struct SimEvent : IComparable<SimEvent>
{
    public DateTime GameTime;
    public int NpcId;
    public SimEventType Type;  // NodeArrival, EncounterCheck, InterruptResolution
    public object Data;
    public int CompareTo(SimEvent other) => GameTime.CompareTo(other.GameTime);
}

public class EventQueue
{
    public void Push(SimEvent evt);
    public SimEvent Pop();
    public bool IsEmpty { get; }
    public DateTime NextTime { get; }
}
```

### 2. NPC Schedule

Tracks each NPC's current state in the DES:

```csharp
public class NpcSchedule
{
    public int NpcId;
    public int Seed;
    public string Role;

    // Current state
    public int CurrentLocationId;
    public string CurrentStorylet;   // e.g., "work_manual"
    public DateTime CurrentStart;     // when this storylet started
    public DateTime CurrentEnd;       // when it will end (arrival time of next node)

    // Assigned locations (from Phase 0A)
    public int HomeLocationId;
    public int WorkplaceLocationId;
    public List<int> SocialVenueIds;

    // Properties
    public Dictionary<string, float> Properties;

    // Relationships (trust per other NPC)
    public Dictionary<int, float> Trust;

    // Position at any point in time (pure function of schedule)
    public Vector3 PositionAt(DateTime gameTime, SpatialModel model);
}
```

### 3. Encounter Resolver

Computes probabilistic encounters from overlapping time-space windows:

```csharp
public class EncounterResolver
{
    // Per-location-type encounter probability (tunable parameters)
    public float P_Venue = 0.07f;
    public float P_Street = 0.015f;
    public float P_Transport = 0.002f;
    public float P_Workplace = 0.04f;
    public float TimeQuantumMinutes = 15f;

    // Track which NPCs are at which locations during which time windows
    public void RegisterPresence(int npcId, int locationId, DateTime from, DateTime to);

    // Check for encounters at a location during a time window
    // Returns list of (npcA, npcB, locationType) encounter events
    public List<EncounterEvent> ResolveEncounters(int locationId, DateTime from, DateTime to);
}
```

**Stay encounters** (two NPCs at same location):
```
Overlap = max(startA, startB) to min(endA, endB)
If overlap > 0:
  duration = overlap in minutes
  P = 1 - (1 - p_location)^(duration / time_quantum)
  Roll random — if hit, encounter fires
```

**Transit encounters** (two NPCs sharing a street segment during travel):
```
Route A and Route B share segment S
Compute temporal overlap on S from travel times
P = f(overlap, segment_type)
```

### 4. DES Main Loop

```csharp
public class DesSimulation
{
    private EventQueue _queue;
    private Dictionary<int, NpcSchedule> _npcs;
    private EncounterResolver _encounters;
    private SpatialModel _spatial;
    private DateTime _clock;

    public void Initialize(SpatialModel model, List<NpcSchedule> npcs)
    {
        // For each NPC: generate starting storylet, push first NodeArrival
    }

    public void RunUntil(DateTime endTime)
    {
        while (!_queue.IsEmpty && _queue.NextTime <= endTime)
        {
            var evt = _queue.Pop();
            _clock = evt.GameTime;

            switch (evt.Type)
            {
                case SimEventType.NodeArrival:
                    ProcessNodeArrival(evt);
                    break;
                case SimEventType.EncounterCheck:
                    ProcessEncounterCheck(evt);
                    break;
                case SimEventType.InterruptResolution:
                    ProcessInterrupt(evt);
                    break;
            }
        }
    }

    private void ProcessNodeArrival(SimEvent evt)
    {
        var npc = _npcs[evt.NpcId];

        // 1. Apply postconditions from completed storylet
        ApplyPostconditions(npc);

        // 2. Select next storylet based on preconditions + properties + time-of-day
        var next = SelectNextStorylet(npc, _clock);

        // 3. Compute spatial verb: destination + travel time
        var destination = ResolveLocation(npc, next);
        var travelTime = _spatial.GetTravelTime(npc.CurrentLocationId, destination);
        var activityDuration = next.Duration;

        // 4. Update NPC state
        npc.CurrentStorylet = next.Id;
        npc.CurrentLocationId = destination;
        npc.CurrentStart = _clock + TimeSpan.FromMinutes(travelTime);
        npc.CurrentEnd = npc.CurrentStart + TimeSpan.FromMinutes(activityDuration);

        // 5. Register presence for encounter detection
        _encounters.RegisterPresence(npc.NpcId, destination, npc.CurrentStart, npc.CurrentEnd);

        // 6. Schedule next NodeArrival
        _queue.Push(new SimEvent {
            GameTime = npc.CurrentEnd,
            NpcId = npc.NpcId,
            Type = SimEventType.NodeArrival
        });

        // 7. Emit event to logger
        EmitNodeArrival(npc, next);
    }
}
```

### 5. Storylet Selection (placeholder)

Phase 0B needs a minimal storylet selector to drive the DES. Use hardcoded schedule templates per role (Phase 1 replaces this with the full storylet library):

```
Worker schedule (time-of-day → storylet):
  06:00 → wake_up (45min, at home)
  07:00 → commute (variable, home → workplace)
  07:30 → work_manual (4h30, at workplace)
  12:00 → lunch_break (30min, at nearest eat shop)
  12:30 → work_manual (4h30, at workplace)
  17:00 → commute (variable, workplace → home or social)
  17:30 → socialize (2h30, at social venue) OR rest (at home)
  20:00 → commute (variable, → home)
  20:30 → sleep (9h30, at home)
```

Property mutations per storylet (hardcoded for now):
- `work_manual`: fatigue +0.28, wealth +0.08
- `sleep`: fatigue → 0.1
- `eat/lunch`: hunger -0.55, wealth -0.03
- Per hour awake: hunger +0.06

## Where Code Lives

The DES engine is **production code**, not testbed-specific:

| Component | Location | Reason |
|-----------|----------|--------|
| `EventQueue` | `JoyceCode/engine/tale/` | Core engine — used by Tier 3 simulation in game |
| `NpcSchedule` | `JoyceCode/engine/tale/` | NPC background state |
| `EncounterResolver` | `JoyceCode/engine/tale/` | Shared encounter logic |
| `DesSimulation` | `JoyceCode/engine/tale/` | Tier 3 simulation driver |
| `SpatialModel` | `JoyceCode/engine/tale/` | Location/route lookup |
| Testbed harness | `Testbed/TestbedMain.cs` | Thin driver that calls `DesSimulation.RunUntil()` |

## Deliverable

`dotnet run --project Testbed -- --days 7` should:
1. Generate cluster + spatial model (from Phase 0A)
2. Create 500 NPCs with schedule-based storylet assignments
3. Run DES for 7 simulated days
4. Print to stdout: total events processed, wall-clock time elapsed, events/second
5. Print sample: 3 NPCs' day-1 storylet sequences (text trace format)

Performance target: 7 days × 500 NPCs should complete in under 1 second.
