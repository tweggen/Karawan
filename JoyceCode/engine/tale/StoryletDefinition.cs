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
    public float DurationMinutesMin;
    public float DurationMinutesMax;
    public string LocationRef; // where to go: "workplace", "home", "nearest_shop_Eat", etc.
    public float Weight;
    public string[] Tags;

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


    public void LoadFromDirectory(string path)
    {
        if (!Directory.Exists(path)) return;
        foreach (var file in Directory.GetFiles(path, "*.json"))
            LoadFromFile(file);
        BuildIndex();
    }


    public void LoadFromFile(string filePath)
    {
        string json = File.ReadAllText(filePath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
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
                def.Postconditions[prop.Name] = prop.Value.GetString();
        }

        // Tags
        if (elem.TryGetProperty("tags", out var tags))
        {
            var tagList = new List<string>();
            foreach (var t in tags.EnumerateArray())
                tagList.Add(t.GetString());
            def.Tags = tagList.ToArray();
        }

        return def;
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

        // Find fallbacks
        _fallbackDay = _all.FirstOrDefault(s => s.Id == "wander");
        _fallbackNight = _all.FirstOrDefault(s => s.Id == "rest");
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
