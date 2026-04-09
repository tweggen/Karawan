using System;
using System.Collections.Generic;
using builtin.tools;
using engine;
using engine.tale;
using static engine.Logger;

namespace nogame.modules.tale;

/// <summary>
/// Handles injection of TALE NPC properties into narration Props system.
/// Provides script resolution fallback.
/// Called from TaleConversationBehavior to manage NPC state during conversations.
/// </summary>
public static class TaleNarrationBindings
{
    private static readonly List<string> _injectedKeys = new();

    /// <summary>
    /// Placeholder for future narration event wiring.
    /// Currently unused but kept for forward compatibility.
    /// </summary>
    public static void Register()
    {
        Trace("TALE NARRATION BINDINGS: Registered");
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
            // Clear previously injected keys
            _injectedKeys.Clear();

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
            Props.Set("npc.role", schedule.Role ?? "unknown");

            // Track injected keys for cleanup
            _injectedKeys.AddRange(new[] {
                "npc.hunger", "npc.anger", "npc.fatigue", "npc.health",
                "npc.wealth", "npc.happiness", "npc.reputation", "npc.morality", "npc.fear",
                "npc.role"
            });

            Trace($"TALE NARRATION BINDINGS: Injected props for NPC {schedule.NpcId}");
        }
        catch (Exception e)
        {
            Error($"TALE NARRATION BINDINGS: Exception injecting props: {e.Message}");
        }
    }

    /// <summary>
    /// Clear all injected NPC properties by resetting them to defaults.
    /// Called after conversation script ends.
    /// Note: Props system doesn't support removal, so we reset to neutral values.
    /// </summary>
    public static void ClearNpcProps()
    {
        try
        {
            // Reset injected properties to neutral/default values
            // This prevents old NPC state from leaking into subsequent conversations
            foreach (var key in _injectedKeys)
            {
                if (key == "npc.role")
                {
                    Props.Set(key, "unknown");
                }
                else
                {
                    Props.Set(key, 0.5f); // Neutral value for numeric props
                }
            }

            _injectedKeys.Clear();
            Trace("TALE NARRATION BINDINGS: Cleared NPC props");
        }
        catch (Exception e)
        {
            Error($"TALE NARRATION BINDINGS: Exception clearing props: {e.Message}");
        }
    }

    /// <summary>
    /// Resolve conversation script using 5-level fallback:
    /// 1. storylet.ConversationScript (explicit override)
    /// 2. tale.{storyletId} (auto-named by id)
    /// 3. First matching tale.tag.{tag} (from storylet tags)
    /// 4. tale.role.{role} (role-specific fallback)
    /// 5. tale.generic (unconditional catch-all)
    /// </summary>
    public static string ResolveScript(StoryletDefinition storylet, string role)
    {
        if (storylet == null)
        {
            Trace("TALE NARRATION BINDINGS: Storylet is null, using tale.generic");
            return "tale.generic";
        }

        // Level 1: Explicit conversation script override
        if (!string.IsNullOrEmpty(storylet.ConversationScript))
        {
            Trace($"TALE NARRATION BINDINGS: Resolved script via explicit override: {storylet.ConversationScript}");
            return storylet.ConversationScript;
        }

        // Level 2: Auto-named by storylet ID
        string idScript = $"tale.{storylet.Id}";
        if (ScriptExists(idScript))
        {
            Trace($"TALE NARRATION BINDINGS: Resolved script via storylet ID: {idScript}");
            return idScript;
        }

        // Level 3: First matching tag
        if (storylet.Tags != null && storylet.Tags.Length > 0)
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

        // Level 4: Role fallback
        if (!string.IsNullOrEmpty(role))
        {
            string roleScript = $"tale.role.{role}";
            if (ScriptExists(roleScript))
            {
                Trace($"TALE NARRATION BINDINGS: Resolved script via role '{role}': {roleScript}");
                return roleScript;
            }
        }

        // Level 5: Generic catch-all
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
