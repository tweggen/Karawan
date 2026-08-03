using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace engine.tale;

public struct PreconditionRange
{
    public float? Min;
    public float? Max;
}

public struct TimeWindow
{
    public TimeSpan Min;
    public TimeSpan Max;

    public bool Contains(TimeSpan time)
    {
        if (Min <= Max)
            return time >= Min && time <= Max;
        // Wraps midnight (e.g., 20:00-06:00)
        return time >= Min || time <= Max;
    }
}

public class InteractionRequestDescriptor
{
    public string Type;                     // "food_delivery", "help", "trade", etc.
    public string LocationRef;              // "current", "home", "workplace", etc.
    public float Urgency;                   // 0.0-1.0
    public int TimeoutMinutes;              // How long before request expires
    public string VariableKey;              // e.g., "$current_request" for storing in NPC state
}

public class InteractionClaimTrigger
{
    public string RequestType;              // What type of request triggers this
    public string[] RoleMatch;              // Roles that can claim (["merchant", "drifter"])
    public string InterruptScope;           // "nest" (pause current, run interaction, resume)
}

public class WaitEdgeDescriptor
{
    public string SignalType;               // "request_fulfilled", "request_failed"
    public string RequestIdVariable;        // "$current_request"
    public int TimeoutMinutes;              // Max wait time
    public string FallbackStorylet;         // If timeout, jump to this
}

public class ConditionalBranch
{
    public Dictionary<string, PreconditionRange>? SelfConditions;       // conditions on self
    public Dictionary<string, PreconditionRange>? TargetConditions;     // conditions on target (keys had "target_" prefix stripped)
    public Dictionary<string, string> Then = new();                     // postconditions: +/-/= format
    public string? StoryletNext;                                        // forced next storylet
}

public class StoryletDefinition
{
    public string Id;
    public string Name;
    public string[] Roles;
    public TimeWindow? TimeOfDay;
    public Dictionary<string, PreconditionRange> PropertyPreconditions;
    public string LocationType; // "workplace", "home", "social_venue", etc.
    public float? DesperationMin;
    public float? MoralityMax;
    public Dictionary<string, string> Postconditions;

    /// <summary>
    /// TALE-SOCIAL Phase E4: group verb fired when this storylet completes.
    /// "form" | "join" | "leave" (null = none). Authored in JSON as
    /// <c>"postconditions": { "group": "form" }</c> but extracted at parse
    /// time so ApplyPostconditions never sees a non-numeric value.
    /// </summary>
    public string GroupAction;

    public float DurationMinutesMin;
    public float DurationMinutesMax;
    public string LocationRef; // where to go: "workplace", "home", "nearest_shop_Eat", etc.
    public float Weight;
    public string[] Tags;

    // Phase 3: Interaction support
    public InteractionRequestDescriptor RequestPostcondition;
    public InteractionClaimTrigger ClaimTrigger;
    public WaitEdgeDescriptor WaitEdge;

    // Phase 5: Conditional postconditions and interrupts
    public List<ConditionalBranch>? PostconditionsIf;
    public int InterruptPriority = 1;   // 1=routine, 5+=escalation trigger, 10=max

    // Phase C2: Storylet-specific conversation script override
    public string? ConversationScript { get; set; }

    public float GetDuration(Random rng)
    {
        if (Math.Abs(DurationMinutesMax - DurationMinutesMin) < 0.1f)
            return DurationMinutesMin;
        return DurationMinutesMin + (float)(rng.NextDouble() * (DurationMinutesMax - DurationMinutesMin));
    }

    public StoryletLocationType ResolveLocationType()
    {
        return LocationRef switch
        {
            "home" => StoryletLocationType.Home,
            "workplace" => StoryletLocationType.Workplace,
            "social_venue" => StoryletLocationType.SocialVenue,
            "nearest_shop_Eat" => StoryletLocationType.EatVenue,
            "random_street" or "street_segment" => StoryletLocationType.Street,
            "current" => StoryletLocationType.Current,
            _ => StoryletLocationType.Current
        };
    }
}


public class StoryletLibrary
{
    private readonly List<StoryletDefinition> _all = new();
    private readonly Dictionary<string, List<StoryletDefinition>> _byRole = new();
    private readonly Dictionary<string, StoryletDefinition> _byId = new();
    private List<StoryletDefinition> _universal = new();
    private StoryletDefinition _fallbackDay;
    private StoryletDefinition _fallbackNight;

    public IReadOnlyList<StoryletDefinition> All => _all;


    /// <summary>
    /// Load every *.json in a directory straight off the filesystem. Only valid
    /// where the model tree exists as real files — the build tools (Chushi's
    /// ScenarioCompiler), Testbed and TestRunner. The game runtime must use
    /// <see cref="LoadFromAssets"/> instead: on Android the storylets live inside
    /// the APK and there is no directory to enumerate.
    /// </summary>
    public void LoadFromDirectory(string path)
    {
        if (!Directory.Exists(path)) return;
        foreach (var file in Directory.GetFiles(path, "*.json"))
            LoadFromFile(file);
        BuildIndex();
    }


    /// <summary>
    /// Load storylets through the asset system, by resource tag. Works on every
    /// platform: desktop resolves the tag via the association registered from
    /// /resources/list, Android reads it out of the APK by its flat logical name.
    /// Returns the number of files successfully read; a tag that cannot be opened
    /// or parsed is skipped and counted in <paramref name="failedTags"/> rather
    /// than aborting the whole library.
    /// </summary>
    /// <summary>
    /// The resource type that marks a storylet library in /resources/list.
    /// One declaration serves two consumers: the resource compiler ships the
    /// file, and <see cref="CollectDeclaredTags"/> discovers it at runtime — so
    /// what is packaged and what is loaded cannot drift apart.
    /// </summary>
    public const string ResourceType = "taleStorylet";


    /// <summary>
    /// Asset tags of every storylet library declared in /resources/list. The tag
    /// is the entry's explicit "tag" or, as everywhere else in the resource
    /// system, the last path component of its uri — which is also the flat
    /// LogicalName the file carries inside the APK.
    ///
    /// Returns an empty list where no config is loaded at all (bare unit-test
    /// fixtures); callers that can fall back to the filesystem should.
    /// </summary>
    public static List<string> CollectDeclaredTags()
    {
        var tags = new List<string>();

        casette.Loader loader;
        try { loader = I.Get<casette.Loader>(); }
        catch (Exception) { return tags; }

        // Config is interpreted before modules activate, so this callback runs
        // synchronously; WhenLoaded still keeps us correct if it is not.
        loader.WhenLoaded("/resources/list", (path, node) =>
        {
            if (node is not System.Text.Json.Nodes.JsonArray arr) return;

            foreach (var resNode in arr)
            {
                if (resNode?["type"]?.GetValue<string>() != ResourceType) continue;

                string tag = resNode?["tag"]?.GetValue<string>();
                if (string.IsNullOrEmpty(tag))
                {
                    string uri = resNode?["uri"]?.GetValue<string>();
                    if (string.IsNullOrEmpty(uri)) continue;
                    tag = Path.GetFileName(uri);
                }

                if (!string.IsNullOrEmpty(tag)) tags.Add(tag);
            }
        }, 100);

        return tags;
    }


    public int LoadFromAssets(IEnumerable<string> tags, out List<string> failedTags)
    {
        failedTags = new List<string>();
        int loaded = 0;

        foreach (var tag in tags)
        {
            try
            {
                using var stream = engine.Assets.Open(tag);
                if (stream == null)
                {
                    failedTags.Add(tag);
                    continue;
                }

                using var reader = new StreamReader(stream);
                LoadFromString(reader.ReadToEnd());
                loaded++;
            }
            catch (Exception)
            {
                failedTags.Add(tag);
            }
        }

        BuildIndex();
        return loaded;
    }


    public void LoadFromFile(string filePath)
    {
        LoadFromString(File.ReadAllText(filePath));
    }


    public void LoadFromString(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var elem in root.EnumerateArray())
                _all.Add(ParseStorylet(elem));
        }
    }


    private static StoryletDefinition ParseStorylet(JsonElement elem)
    {
        var def = new StoryletDefinition
        {
            Id = elem.GetProperty("id").GetString(),
            Name = elem.TryGetProperty("name", out var n) ? n.GetString() : null,
            Weight = elem.TryGetProperty("weight", out var w) ? w.GetSingle() : 1.0f,
            LocationRef = elem.TryGetProperty("location", out var loc) ? loc.GetString() : "current",
            PropertyPreconditions = new Dictionary<string, PreconditionRange>(),
            Postconditions = new Dictionary<string, string>(),
            Tags = Array.Empty<string>()
        };

        // Roles
        if (elem.TryGetProperty("roles", out var roles))
        {
            var list = new List<string>();
            foreach (var r in roles.EnumerateArray())
                list.Add(r.GetString());
            def.Roles = list.ToArray();
        }
        else
        {
            def.Roles = Array.Empty<string>();
        }

        // Duration
        if (elem.TryGetProperty("duration_minutes", out var dur))
        {
            def.DurationMinutesMin = dur.GetSingle();
            def.DurationMinutesMax = dur.GetSingle();
        }
        if (elem.TryGetProperty("duration_minutes_min", out var dmin))
            def.DurationMinutesMin = dmin.GetSingle();
        if (elem.TryGetProperty("duration_minutes_max", out var dmax))
            def.DurationMinutesMax = dmax.GetSingle();

        // Time of day
        if (elem.TryGetProperty("time_of_day", out var tod))
        {
            var tw = new TimeWindow();
            if (tod.TryGetProperty("min", out var tmin))
                tw.Min = TimeSpan.Parse(tmin.GetString());
            if (tod.TryGetProperty("max", out var tmax))
                tw.Max = TimeSpan.Parse(tmax.GetString());
            def.TimeOfDay = tw;
        }

        // Property preconditions
        if (elem.TryGetProperty("preconditions", out var precs))
        {
            foreach (var prop in precs.EnumerateObject())
            {
                if (prop.Name == "time_of_day") continue;
                if (prop.Name == "desperation_min")
                {
                    def.DesperationMin = prop.Value.GetSingle();
                    continue;
                }
                if (prop.Name == "morality_max")
                {
                    def.MoralityMax = prop.Value.GetSingle();
                    continue;
                }
                if (prop.Value.ValueKind == JsonValueKind.Object)
                {
                    var range = new PreconditionRange();
                    if (prop.Value.TryGetProperty("min", out var pmin))
                        range.Min = pmin.GetSingle();
                    if (prop.Value.TryGetProperty("max", out var pmax))
                        range.Max = pmax.GetSingle();
                    def.PropertyPreconditions[prop.Name] = range;
                }
            }
        }

        // Location type precondition
        if (elem.TryGetProperty("location_type", out var lt))
            def.LocationType = lt.GetString();

        // Postconditions
        if (elem.TryGetProperty("postconditions", out var posts))
        {
            foreach (var prop in posts.EnumerateObject())
            {
                // Skip nested objects (request, signal) - those are handled in Phase 3 sections
                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    // Phase E4 group verb: not a numeric property delta —
                    // extract it so ApplyPostconditions never float.Parses it.
                    if (prop.Name == "group")
                        def.GroupAction = prop.Value.GetString();
                    else
                        def.Postconditions[prop.Name] = prop.Value.GetString();
                }
            }
        }

        // Tags
        if (elem.TryGetProperty("tags", out var tags))
        {
            var tagList = new List<string>();
            foreach (var t in tags.EnumerateArray())
                tagList.Add(t.GetString());
            def.Tags = tagList.ToArray();
        }

        // Phase 3: Interaction postconditions
        if (elem.TryGetProperty("postconditions", out var postsElem) && postsElem.TryGetProperty("request", out var reqPost))
        {
            def.RequestPostcondition = new InteractionRequestDescriptor();
            if (reqPost.TryGetProperty("type", out var reqType))
                def.RequestPostcondition.Type = reqType.GetString();
            if (reqPost.TryGetProperty("location", out var reqLoc))
                def.RequestPostcondition.LocationRef = reqLoc.GetString();
            if (reqPost.TryGetProperty("urgency", out var reqUrg))
                def.RequestPostcondition.Urgency = reqUrg.GetSingle();
            if (reqPost.TryGetProperty("timeout_minutes", out var reqTimeout))
                def.RequestPostcondition.TimeoutMinutes = reqTimeout.GetInt32();
            if (reqPost.TryGetProperty("variable_key", out var reqVar))
                def.RequestPostcondition.VariableKey = reqVar.GetString();
        }

        // Phase 3: Claim triggers
        if (elem.TryGetProperty("claim_trigger", out var claimTrig))
        {
            def.ClaimTrigger = new InteractionClaimTrigger();
            if (claimTrig.TryGetProperty("request_type", out var ctReqType))
                def.ClaimTrigger.RequestType = ctReqType.GetString();
            if (claimTrig.TryGetProperty("role_match", out var ctRoles))
            {
                var roleList = new List<string>();
                foreach (var r in ctRoles.EnumerateArray())
                    roleList.Add(r.GetString());
                def.ClaimTrigger.RoleMatch = roleList.ToArray();
            }
            if (claimTrig.TryGetProperty("interrupt_scope", out var ctScope))
                def.ClaimTrigger.InterruptScope = ctScope.GetString();
        }

        // Phase 3: Wait edges
        if (elem.TryGetProperty("wait_edge", out var waitE))
        {
            def.WaitEdge = new WaitEdgeDescriptor();
            if (waitE.TryGetProperty("signal_type", out var weSig))
                def.WaitEdge.SignalType = weSig.GetString();
            if (waitE.TryGetProperty("request_id", out var weReqId))
                def.WaitEdge.RequestIdVariable = weReqId.GetString();
            if (waitE.TryGetProperty("timeout_minutes", out var weTimeout))
                def.WaitEdge.TimeoutMinutes = weTimeout.GetInt32();
            if (waitE.TryGetProperty("fallback_storylet", out var weFall))
                def.WaitEdge.FallbackStorylet = weFall.GetString();
        }

        // Phase 5: Conditional postconditions
        if (elem.TryGetProperty("postconditions_if", out var pif))
            def.PostconditionsIf = ParseConditionalBranches(pif);

        // Phase 5: Interrupt priority
        if (elem.TryGetProperty("interrupt_priority", out var ip))
            def.InterruptPriority = ip.GetInt32();

        // Phase C2: Explicit conversation script override
        if (elem.TryGetProperty("conversation_script", out var cs))
            def.ConversationScript = cs.GetString();

        return def;
    }


    private static List<ConditionalBranch> ParseConditionalBranches(JsonElement arr)
    {
        var branches = new List<ConditionalBranch>();
        if (arr.ValueKind != JsonValueKind.Array) return branches;

        foreach (var elem in arr.EnumerateArray())
        {
            var branch = new ConditionalBranch();

            // Parse condition (splits into self/target based on key prefix)
            if (elem.TryGetProperty("condition", out var condObj) && condObj.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in condObj.EnumerateObject())
                {
                    var range = new PreconditionRange();
                    if (prop.Value.ValueKind == JsonValueKind.Object)
                    {
                        if (prop.Value.TryGetProperty("min", out var pmin))
                            range.Min = pmin.GetSingle();
                        if (prop.Value.TryGetProperty("max", out var pmax))
                            range.Max = pmax.GetSingle();
                    }

                    if (prop.Name.StartsWith("target_"))
                    {
                        if (branch.TargetConditions == null)
                            branch.TargetConditions = new();
                        branch.TargetConditions[prop.Name.Substring("target_".Length)] = range;
                    }
                    else
                    {
                        if (branch.SelfConditions == null)
                            branch.SelfConditions = new();
                        branch.SelfConditions[prop.Name] = range;
                    }
                }
            }

            // Parse then (postconditions)
            if (elem.TryGetProperty("then", out var thenObj) && thenObj.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in thenObj.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.String)
                        branch.Then[prop.Name] = prop.Value.GetString();
                }
            }

            // Parse storylet_next
            if (elem.TryGetProperty("storylet_next", out var sn))
                branch.StoryletNext = sn.GetString();

            branches.Add(branch);
        }

        return branches;
    }


    private void BuildIndex()
    {
        _byRole.Clear();
        _byId.Clear();
        _universal = new List<StoryletDefinition>();

        foreach (var def in _all)
        {
            _byId[def.Id] = def;

            if (def.Roles == null || def.Roles.Length == 0)
            {
                _universal.Add(def);
            }
            else
            {
                foreach (var role in def.Roles)
                {
                    string key = role.ToLowerInvariant();
                    if (!_byRole.TryGetValue(key, out var list))
                    {
                        list = new List<StoryletDefinition>();
                        _byRole[key] = list;
                    }
                    list.Add(def);
                }
            }
        }

        // Find fallbacks - REQUIRED for safety
        _fallbackDay = _all.FirstOrDefault(s => s.Id == "wander");
        _fallbackNight = _all.FirstOrDefault(s => s.Id == "rest");

        // Fatal error if fallbacks are missing
        if (_fallbackDay == null || _fallbackNight == null)
        {
            var missing = new List<string>();
            if (_fallbackDay == null) missing.Add("wander");
            if (_fallbackNight == null) missing.Add("rest");

            var loaded = string.Join(", ", _all.Select(s => s.Id).Take(20));
            throw new InvalidOperationException(
                $"FATAL: StoryletLibrary missing required fallback storylets: {string.Join(", ", missing)}. " +
                $"Total loaded: {_all.Count}. " +
                $"First 20 IDs: {loaded}");
        }
    }


    public List<StoryletDefinition> GetCandidates(string role)
    {
        var result = new List<StoryletDefinition>(_universal);
        string key = role.ToLowerInvariant();
        if (_byRole.TryGetValue(key, out var roleSpecific))
            result.AddRange(roleSpecific);
        return result;
    }


    public StoryletDefinition GetById(string id) =>
        _byId.GetValueOrDefault(id);


    public StoryletDefinition GetFallback(TimeSpan timeOfDay)
    {
        // Night: 22:00-05:00 → rest, otherwise wander
        if (timeOfDay.Hours >= 22 || timeOfDay.Hours < 5)
            return _fallbackNight ?? _fallbackDay;
        return _fallbackDay ?? _fallbackNight;
    }
}
