using System;
using System.Collections.Generic;
using builtin.tools;
using engine;
using engine.narration;
using engine.tale;
using nogame.modules.story;
using static engine.Logger;

namespace nogame.modules.tale;

/// <summary>
/// Handles injection of TALE NPC properties into narration Props system.
/// Provides script resolution fallback and cleanup handlers.
/// Called during TaleModule activation to wire up conversation system.
/// </summary>
public static class TaleNarrationBindings
{
    /// <summary>
    /// Register bindings with narration system.
    /// Called once from TaleModule.OnModuleActivate().
    /// </summary>
    public static void Register(Narration narration)
    {
        if (narration == null)
        {
            Warning("TALE NARRATION BINDINGS: Narration manager is null, cannot register");
            return;
        }

        // Subscribe to script ended event to clean up injected props
        narration.Subscribe(engine.narration.Narration.ScriptEndedEvent.EVENT_TYPE,
            (sender, args) => ClearNpcProps());

        Trace("TALE NARRATION BINDINGS: Registered with narration manager");
    }

    /// <summary>
    /// Inject TALE NPC properties into narration Props system.
    /// Called before conversation script starts.
    /// Properties are namespaced as "npc.{propertyName}" to avoid collisions.
    /// </summary>
    public static void InjectNpcProps(NpcSchedule schedule)
    {
        if (schedule == null)
        {
            Warning("TALE NARRATION BINDINGS: Schedule is null, cannot inject props");
            return;
        }

        try
        {
            // Core properties always injected
            Props.Set("npc.hunger", schedule.Properties.GetValueOrDefault("hunger", 0.5f));
            Props.Set("npc.anger", schedule.Properties.GetValueOrDefault("anger", 0.5f));
            Props.Set("npc.fatigue", schedule.Properties.GetValueOrDefault("fatigue", 0.5f));
            Props.Set("npc.health", schedule.Properties.GetValueOrDefault("health", 1f));
            Props.Set("npc.wealth", schedule.Properties.GetValueOrDefault("wealth", 0.5f));
            Props.Set("npc.happiness", schedule.Properties.GetValueOrDefault("happiness", 0.5f));
            Props.Set("npc.reputation", schedule.Properties.GetValueOrDefault("reputation", 0.5f));
            Props.Set("npc.morality", schedule.Properties.GetValueOrDefault("morality", 0.5f));
            Props.Set("npc.fear", schedule.Properties.GetValueOrDefault("fear", 0.5f));

            // Role and context
            Props.Set("npc.role", schedule.Role ?? "unknown");

            Trace($"TALE NARRATION BINDINGS: Injected props for NPC {schedule.NpcId}");
        }
        catch (Exception e)
        {
            Error($"TALE NARRATION BINDINGS: Exception injecting props: {e.Message}");
        }
    }

    /// <summary>
    /// Clear all injected NPC properties from narration Props system.
    /// Called on ScriptEndedEvent.
    /// </summary>
    public static void ClearNpcProps()
    {
        try
        {
            string[] keysToRemove = new[]
            {
                "npc.hunger", "npc.anger", "npc.fatigue", "npc.health",
                "npc.wealth", "npc.happiness", "npc.reputation", "npc.morality", "npc.fear",
                "npc.role"
            };

            foreach (var key in keysToRemove)
            {
                Props.Remove(key);
            }

            Trace("TALE NARRATION BINDINGS: Cleared NPC props");
        }
        catch (Exception e)
        {
            Error($"TALE NARRATION BINDINGS: Exception clearing props: {e.Message}");
        }
    }

    /// <summary>
    /// Resolve conversation script using 4-level fallback:
    /// 1. tale.{storyletId} (auto-named by id)
    /// 2. First matching tale.tag.{tag} (from storylet tags)
    /// 3. tale.role.{role} (role-specific fallback)
    /// 4. tale.generic (unconditional catch-all)
    ///
    /// Note: Phase C2 will add level 0 (explicit ConversationScript field)
    /// </summary>
    public static string ResolveScript(StoryletDefinition storylet, string role)
    {
        if (storylet == null)
        {
            Trace("TALE NARRATION BINDINGS: Storylet is null, using tale.generic");
            return "tale.generic";
        }

        // Level 1: Auto-named by storylet ID
        string idScript = $"tale.{storylet.Id}";
        if (ScriptExists(idScript))
        {
            Trace($"TALE NARRATION BINDINGS: Resolved script via storylet ID: {idScript}");
            return idScript;
        }

        // Level 2: First matching tag
        if (storylet.Tags != null && storylet.Tags.Count > 0)
        {
            foreach (var tag in storylet.Tags)
            {
                string tagScript = $"tale.tag.{tag}";
                if (ScriptExists(tagScript))
                {
                    Trace($"TALE NARRATION BINDINGS: Resolved script via tag '{tag}': {tagScript}");
                    return tagScript;
                }
            }
        }

        // Level 3: Role fallback
        if (!string.IsNullOrEmpty(role))
        {
            string roleScript = $"tale.role.{role}";
            if (ScriptExists(roleScript))
            {
                Trace($"TALE NARRATION BINDINGS: Resolved script via role '{role}': {roleScript}");
                return roleScript;
            }
        }

        // Level 4: Generic catch-all
        Trace("TALE NARRATION BINDINGS: Resolved script via generic fallback");
        return "tale.generic";
    }

    /// <summary>
    /// Check if a script exists in the narration system.
    /// (Placeholder - optimistically assumes scripts exist if they follow pattern)
    /// </summary>
    private static bool ScriptExists(string scriptName)
    {
        // TODO: Implement actual script existence check via narration system
        // For now, scripts are assumed to exist if loaded via __include__
        return true;
    }
}
