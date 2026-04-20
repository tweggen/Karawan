using System;
using System.Collections.Generic;
using System.Globalization;

namespace engine.tale;

public enum StoryletLocationType
{
    Home,
    Workplace,
    SocialVenue,
    EatVenue,
    Street,
    Current
}

/// <summary>
/// JSON-driven storylet selector. Evaluates preconditions against NPC state
/// and selects via weighted random with seed determinism.
/// </summary>
public class StoryletSelector
{
    private readonly StoryletLibrary _library;

    /// <summary>Background hunger accumulation rate per hour awake.</summary>
    public float HungerPerHour = 0.04f;

    /// <summary>Default properties all NPCs should have (for postcondition safety).</summary>
    private static readonly string[] DefaultPropertyKeys =
    {
        "hunger", "health", "fatigue", "anger", "fear", "trust", "happiness", "reputation", "morality", "wealth"
    };


    public StoryletSelector(StoryletLibrary library)
    {
        _library = library;
    }


    /// <summary>
    /// Ensure NPC has all expected properties initialized (backfill missing ones with defaults).
    /// Fixes issues where NPCs loaded from older saves are missing properties added later.
    /// </summary>
    public static void EnsurePropertiesInitialized(NpcSchedule npc)
    {
        if (npc?.Properties == null)
            npc.Properties = new Dictionary<string, float>();

        foreach (var key in DefaultPropertyKeys)
        {
            if (!npc.Properties.ContainsKey(key))
                npc.Properties[key] = 0.5f;  // Default to neutral
        }
    }


    public static float ComputeDesperation(NpcSchedule npc)
    {
        float hunger = npc.Properties.GetValueOrDefault("hunger", 0f);
        float wealth = npc.Properties.GetValueOrDefault("wealth", 0.5f);
        float anger = npc.Properties.GetValueOrDefault("anger", 0f);
        float health = npc.Properties.GetValueOrDefault("health", 1f);
        return Math.Clamp(hunger * 0.4f + (1f - wealth) * 0.3f + anger * 0.2f + (1f - health) * 0.1f, 0f, 1f);
    }


    public StoryletDefinition SelectNext(NpcSchedule npc, DateTime currentTime)
    {
        var timeOfDay = currentTime.TimeOfDay;
        float desperation = ComputeDesperation(npc);
        float morality = npc.Properties.GetValueOrDefault("morality", 0.7f);

        var pool = _library.GetCandidates(npc.Role);
        var candidates = new List<StoryletDefinition>();

        foreach (var def in pool)
        {
            if (def == null)
                continue;  // Skip null entries (defensive)
            if (!PassesPreconditions(def, npc, timeOfDay, desperation, morality))
                continue;
            candidates.Add(def);
        }

        if (candidates.Count == 0)
        {
            var fallback = _library.GetFallback(timeOfDay);
            if (fallback != null)
                return fallback;
            // If fallback is also null, this is a critical configuration error
            throw new InvalidOperationException($"StoryletSelector: no candidates and no fallback for role={npc.Role} at time {timeOfDay}. Check your storylet configuration.");
        }

        // Weighted random selection with seed determinism
        var rng = new Random(npc.Seed + npc.ScheduleStep * 7919);
        var selected = WeightedSelect(candidates, rng);

        // If WeightedSelect fails (all weights zero), use first candidate
        if (selected == null && candidates.Count > 0)
        {
            selected = candidates[0];
        }

        return selected;
    }


    private static bool PassesPreconditions(StoryletDefinition def, NpcSchedule npc,
        TimeSpan timeOfDay, float desperation, float morality)
    {
        // Time of day
        if (def.TimeOfDay.HasValue && !def.TimeOfDay.Value.Contains(timeOfDay))
            return false;

        // Desperation gate
        if (def.DesperationMin.HasValue && desperation < def.DesperationMin.Value)
            return false;

        // Morality gate
        if (def.MoralityMax.HasValue && morality > def.MoralityMax.Value)
            return false;

        // Property range preconditions
        foreach (var (prop, range) in def.PropertyPreconditions)
        {
            if (prop == "in_group") continue;  // Handle in_group separately below

            float value = npc.Properties.GetValueOrDefault(prop, 0.5f);
            if (range.Min.HasValue && value < range.Min.Value) return false;
            if (range.Max.HasValue && value > range.Max.Value) return false;
        }

        // in_group precondition
        if (def.PropertyPreconditions.ContainsKey("in_group") && npc.GroupId == -1)
            return false;

        // Location feasibility: skip if NPC can't reach the location
        if (def.LocationRef == "workplace" && npc.WorkplaceLocationId < 0) return false;
        if (def.LocationRef == "home" && npc.HomeLocationId < 0) return false;
        if (def.LocationRef == "social_venue" && (npc.SocialVenueIds == null || npc.SocialVenueIds.Count == 0))
            return false;

        return true;
    }


    private static StoryletDefinition WeightedSelect(List<StoryletDefinition> candidates, Random rng)
    {
        if (candidates == null || candidates.Count == 0)
            return null;

        // Make a local snapshot to avoid concurrent modification issues during long selection
        var snapshot = new List<StoryletDefinition>(candidates.Count);
        foreach (var c in candidates)
        {
            if (c != null)
                snapshot.Add(c);
        }

        if (snapshot.Count == 0)
            return null;

        float totalWeight = 0f;
        foreach (var def in snapshot)
        {
            totalWeight += def.Weight;
        }

        if (totalWeight <= 0f)
            return snapshot.Count > 0 ? snapshot[0] : null;  // Default to first if all weights are zero

        float roll = (float)(rng.NextDouble() * totalWeight);
        float cumulative = 0f;
        foreach (var def in snapshot)
        {
            cumulative += def.Weight;
            if (roll < cumulative)
                return def;
        }

        // Return last candidate
        return snapshot.Count > 0 ? snapshot[snapshot.Count - 1] : null;
    }


    /// <summary>
    /// Apply postconditions from a completed storylet.
    /// Returns a dictionary of property deltas for logging.
    /// </summary>
    public Dictionary<string, float> ApplyPostconditions(NpcSchedule npc,
        StoryletDefinition storylet, float durationMinutes, Dictionary<string, float> deltasBuffer)
    {
        deltasBuffer.Clear();

        // Ensure all expected properties exist (fixes missing keys from older saves)
        EnsurePropertiesInitialized(npc);

        // Apply storylet postconditions
        foreach (var (prop, expr) in storylet.Postconditions)
        {
            if (expr.StartsWith("="))
            {
                float value = float.Parse(expr.AsSpan(1), CultureInfo.InvariantCulture);
                RecordSet(npc, prop, value, deltasBuffer);
            }
            else
            {
                float delta = float.Parse(expr, CultureInfo.InvariantCulture);
                RecordDelta(npc, prop, delta, deltasBuffer);
            }
        }

        // Background hunger tick (unless storylet already modified hunger)
        if (!storylet.Postconditions.ContainsKey("hunger"))
        {
            float hoursAwake = durationMinutes / 60f;
            RecordDelta(npc, "hunger", HungerPerHour * hoursAwake, deltasBuffer);
        }

        return deltasBuffer;
    }


    /// <summary>
    /// Legacy overload for compatibility with DesSimulation which passes storylet ID string.
    /// Falls back to hunger tick only when storylet definition is not found.
    /// </summary>
    public Dictionary<string, float> ApplyPostconditions(NpcSchedule npc, string storyletId,
        float durationMinutes, Dictionary<string, float> deltasBuffer)
    {
        deltasBuffer.Clear();

        // Ensure all expected properties exist (fixes missing keys from older saves)
        EnsurePropertiesInitialized(npc);

        // Just apply hunger tick as fallback
        float hoursAwake = durationMinutes / 60f;
        RecordDelta(npc, "hunger", HungerPerHour * hoursAwake, deltasBuffer);
        return deltasBuffer;
    }


    private static void RecordDelta(NpcSchedule npc, string prop, float delta,
        Dictionary<string, float> deltas)
    {
        // Defensive: ensure Properties dict exists (shouldn't be necessary after EnsurePropertiesInitialized, but safe)
        if (npc?.Properties == null)
            npc.Properties = new Dictionary<string, float>();

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
        // Defensive: ensure Properties dict exists (shouldn't be necessary after EnsurePropertiesInitialized, but safe)
        if (npc?.Properties == null)
            npc.Properties = new Dictionary<string, float>();

        float old = npc.Properties.GetValueOrDefault(prop, 0.5f);
        npc.Properties[prop] = Math.Clamp(value, 0f, 1f);
        deltas[prop] = npc.Properties[prop] - old;
    }


    /// <summary>
    /// Apply conditional postconditions based on self and target NPC properties.
    /// Evaluates postconditions_if branches in order, applies effects from the first matching branch,
    /// and returns the forced next storylet ID (if specified).
    /// </summary>
    public static string? ApplyConditionalPostconditions(
        StoryletDefinition def, NpcSchedule self, NpcSchedule? target)
    {
        if (def?.PostconditionsIf == null || def.PostconditionsIf.Count == 0)
            return null;

        // Ensure all expected properties exist (fixes missing keys from older saves)
        EnsurePropertiesInitialized(self);
        if (target != null)
            EnsurePropertiesInitialized(target);

        var deltasBuf = new Dictionary<string, float>();

        foreach (var branch in def.PostconditionsIf)
        {
            // Evaluate self conditions
            bool selfMatches = true;
            if (branch.SelfConditions != null)
            {
                foreach (var (prop, range) in branch.SelfConditions)
                {
                    float value = self.Properties.GetValueOrDefault(prop, 0.5f);
                    if (range.Min.HasValue && value < range.Min.Value)
                    {
                        selfMatches = false;
                        break;
                    }
                    if (range.Max.HasValue && value > range.Max.Value)
                    {
                        selfMatches = false;
                        break;
                    }
                }
            }

            if (!selfMatches) continue;

            // Evaluate target conditions
            bool targetMatches = true;
            if (branch.TargetConditions != null)
            {
                if (target == null) targetMatches = false;
                else
                {
                    foreach (var (prop, range) in branch.TargetConditions)
                    {
                        float value = target.Properties.GetValueOrDefault(prop, 0.5f);
                        if (range.Min.HasValue && value < range.Min.Value)
                        {
                            targetMatches = false;
                            break;
                        }
                        if (range.Max.HasValue && value > range.Max.Value)
                        {
                            targetMatches = false;
                            break;
                        }
                    }
                }
            }

            if (!targetMatches) continue;

            // Both self and target conditions matched: apply postconditions
            foreach (var (prop, expr) in branch.Then)
            {
                string targetProp = prop;
                NpcSchedule? targetNpc = self;

                // Check if this targets the other NPC (props starting with "target_" go to target NPC)
                if (prop.StartsWith("target_"))
                {
                    if (target == null) continue;
                    targetProp = prop.Substring("target_".Length);
                    targetNpc = target;
                }

                // Apply the delta/set
                if (expr.StartsWith("="))
                {
                    float value = float.Parse(expr.AsSpan(1), CultureInfo.InvariantCulture);
                    RecordSet(targetNpc, targetProp, value, deltasBuf);
                }
                else
                {
                    float delta = float.Parse(expr, CultureInfo.InvariantCulture);
                    RecordDelta(targetNpc, targetProp, delta, deltasBuf);
                }
            }

            // First matching branch found and applied
            return branch.StoryletNext;
        }

        return null;
    }
}
