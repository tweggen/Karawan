using System;
using System.Collections.Generic;
using builtin.tools;
using engine;
using engine.narration;
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
    private static NpcSchedule? _currentSchedule;

    /// <summary>
    /// TALE S1-c: the running conversation, from BeginSession until its script
    /// ends (ClearNpcProps). Logical thread only.
    /// </summary>
    public static ConversationSession? Session { get; private set; }

    /// <summary>The game clock for interactions applied from here; set by TaleModule.</summary>
    public static Func<DateTime>? GameNowProvider { get; set; }

    public static DateTime GameNow()
    {
        try { return GameNowProvider?.Invoke() ?? global::nogame.GameState.GameT0; }
        catch { return global::nogame.GameState.GameT0; }
    }

    /// <summary>
    /// Placeholder for future narration event wiring.
    /// Event subscription is handled directly in TaleModule.OnModuleActivate().
    /// </summary>
    public static void Register()
    {
        Trace("TALE NARRATION BINDINGS: Registered");
    }

    /// <summary>
    /// Get the current NPC schedule being conversed with.
    /// Phase C4: Used by TaleModule to access schedule during post-conversation updates.
    /// </summary>
    public static NpcSchedule? GetCurrentSchedule()
    {
        return _currentSchedule;
    }

    /// <summary>
    /// Set the current NPC schedule (called during conversation setup).
    /// Phase C4: Internal-only via InjectNpcProps().
    /// </summary>
    internal static void SetCurrentSchedule(NpcSchedule? schedule)
    {
        _currentSchedule = schedule;
    }

    private static readonly StandingPublisher _standingPublisher = new();

    /// <summary>
    /// TALE S1-f: publish the player's standing level as props.player.standing
    /// (name) and props.player.standing_rank (0–3) — only when it changed.
    /// TaleModule publishes the player's cluster once a second; a conversation
    /// publishes its NPC's cluster when it starts. Logical thread.
    /// </summary>
    public static void PublishStanding(StandingReading reading)
    {
        if (!_standingPublisher.Changed(reading.Level)) return;
        Props.Set("player.standing", reading.Name);
        Props.Set("player.standing_rank", (float)(int)reading.Level);
        Trace($"TALE NARRATION BINDINGS: player standing now {reading.Name} ({reading.Inputs})");
    }


    /// <summary>TALE S1-c: a conversation with <paramref name="npcId"/> runs <paramref name="scriptName"/>.</summary>
    public static void BeginSession(int npcId, string scriptName)
    {
        Session = new ConversationSession(npcId, scriptName);
    }


    /// <summary>
    /// The player's cash, or null before autosave has a game state (its getter
    /// throws until then).
    /// </summary>
    public static global::nogame.GameState? TryGetGameState()
    {
        try { return I.Get<global::nogame.modules.AutoSave>().GameState; }
        catch { return null; }
    }


    /// <summary>
    /// TALE S1-c: refresh what an interaction inside the conversation changed,
    /// so later nodes of the same script see it: trust, met, wealth, cash.
    /// </summary>
    public static void RefreshPlayerProps(NpcSchedule? schedule)
    {
        if (schedule != null)
        {
            Props.Set("npc.met_player", PlayerTrust.Knows(schedule) ? "true" : "false");
            Props.Set("npc.trust_player", PlayerTrust.Get(schedule));
            Props.Set("npc.wealth", schedule.Properties?.GetValueOrDefault("wealth", 0.5f) ?? 0.5f);
        }
        Props.Set("player.cash", (float)(TryGetGameState()?.Cash ?? 0));
    }


    /// <summary>
    /// TALE S1-c: the player hit or rammed NPC <paramref name="npcId"/>. If it is
    /// the conversation partner, the conversation is cancelled by violence
    /// (no chat at its end), the action is applied, and the dialogue closes.
    /// Returns whether it was the partner. Logical thread.
    /// </summary>
    public static bool EndByViolence(int npcId, PlayerAction action, string reason)
    {
        try
        {
            if (!PlayerConversation.OnViolence(I.Get<TaleManager>(), Session, npcId, action, GameNow()))
                return false;
            Trace($"TALE NARRATION BINDINGS: conversation with NPC {npcId} ended by violence ({reason}, {action})");
            I.Get<global::nogame.modules.story.Narration>().CancelConversation();
            return true;
        }
        catch (Exception e)
        {
            Error($"TALE NARRATION BINDINGS: Exception ending conversation by violence: {e.Message}");
            return false;
        }
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

            // Phase C4: track current NPC schedule for post-conversation update
            SetCurrentSchedule(schedule);

            // Trust / relationship props
            schedule.Trust ??= new Dictionary<int, float>();
            bool hasMetPlayer = PlayerTrust.Knows(schedule);
            float trustForPlayer = PlayerTrust.Get(schedule);
            Props.Set("npc.met_player", hasMetPlayer ? "true" : "false");
            Props.Set("npc.trust_player", trustForPlayer);
            Props.Set("npc.group_id", (float)schedule.GroupId);
            Props.Set("player.cash", (float)(TryGetGameState()?.Cash ?? 0));

            // Phase E5: faction props from the runtime group table, so
            // dialogue can reference the NPC's group by name and type.
            bool hasGroup = false;
            string groupType = "none";
            string groupName = "";
            float groupSize = 0f;
            if (schedule.GroupId >= 0)
            {
                try
                {
                    var group = I.Get<TaleManager>().GetGroup(schedule.ClusterIndex, schedule.GroupId);
                    if (group != null)
                    {
                        hasGroup = true;
                        groupType = group.Type ?? "social";
                        groupName = group.Name ?? "";
                        groupSize = group.MemberIds?.Count ?? 0;
                    }
                }
                catch
                {
                    // TaleManager unavailable: leave the ungrouped defaults.
                }
            }
            Props.Set("npc.has_group", hasGroup ? "true" : "false");
            Props.Set("npc.group_type", groupType);
            Props.Set("npc.group_name", groupName);
            Props.Set("npc.group_size", groupSize);

            // TALE S1-e: what the NPC's group thinks of the player, and how many
            // other members know the player. 0 when ungrouped.
            var standing = GroupStanding.None;
            if (hasGroup)
            {
                try { standing = I.Get<TaleManager>().GetGroupStanding(schedule.NpcId); }
                catch { /* TaleManager unavailable: neutral */ }
            }
            Props.Set("npc.group_standing", standing.Standing);
            Props.Set("npc.group_known", (float)standing.Known);

            // TALE S1-f: dialogue reads the standing in the NPC's own cluster.
            try { PublishStanding(I.Get<TaleManager>().GetPlayerStanding(schedule.ClusterIndex)); }
            catch { /* TaleManager unavailable */ }

            // Track injected keys for cleanup
            _injectedKeys.AddRange(new[] {
                "npc.hunger", "npc.anger", "npc.fatigue", "npc.health",
                "npc.wealth", "npc.happiness", "npc.reputation", "npc.morality", "npc.fear",
                "npc.role", "npc.met_player", "npc.trust_player", "npc.group_id",
                "npc.has_group", "npc.group_type", "npc.group_name", "npc.group_size",
                "npc.group_standing", "npc.group_known"
            });

            // Memory flags written by the tale.npc.remember event handler. Exposed to
            // narration scripts as props.npc.player_fact.{factName} == 1.
            if (schedule.Properties != null)
            {
                foreach (var kvp in schedule.Properties)
                {
                    if (!kvp.Key.StartsWith("player_fact.")) continue;
                    string propKey = "npc." + kvp.Key;
                    Props.Set(propKey, kvp.Value);
                    _injectedKeys.Add(propKey);
                }
            }

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
                else if (key == "npc.has_group")
                {
                    Props.Set(key, "false");
                }
                else if (key == "npc.group_type")
                {
                    Props.Set(key, "none");
                }
                else if (key == "npc.group_name")
                {
                    Props.Set(key, "");
                }
                else if (key == "npc.group_size" || key == "npc.group_id")
                {
                    Props.Set(key, key == "npc.group_id" ? -1f : 0f);
                }
                else if (key == "npc.group_standing" || key == "npc.group_known")
                {
                    Props.Set(key, 0f); // S1-e: neutral is 0, not the 0.5 of normalised stats
                }
                else if (key.StartsWith("npc.player_fact."))
                {
                    Props.Set(key, 0f); // Memory flags are 0/1, not normalized stats
                }
                else
                {
                    Props.Set(key, 0.5f); // Neutral value for numeric props
                }
            }

            _injectedKeys.Clear();
            SetCurrentSchedule(null);
            Session = null;
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
        string script = ConversationScriptResolver.Resolve(storylet, role, ScriptExists);
        Trace($"TALE NARRATION BINDINGS: Resolved script {script} for storylet {storylet?.Id ?? "(none)"}, role {role}");
        return script;
    }


    private static bool ScriptExists(string scriptName)
    {
        try
        {
            return I.Get<NarrationManager>().HasScript(scriptName);
        }
        catch
        {
            return false;
        }
    }
}
