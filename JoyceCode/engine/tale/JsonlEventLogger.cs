using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace engine.tale;

/// <summary>
/// Writes DES events as JSON Lines to a file.
/// Each method formats one JSON line and writes it to the underlying StreamWriter.
/// </summary>
public class JsonlEventLogger : IEventLogger, IDisposable
{
    private readonly StreamWriter _writer;
    private readonly StringBuilder _sb = new(512);
    private readonly DateTime _startTime;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false
    };


    public JsonlEventLogger(string filePath, DateTime startTime)
    {
        _startTime = startTime;
        _writer = new StreamWriter(filePath, false, Encoding.UTF8, 131072);
    }


    private int DayOf(DateTime t) => (int)(t.Date - _startTime.Date).TotalDays + 1;


    public void LogNpcCreated(int npcId, int seed, string role, int homeLocationId,
        int workplaceLocationId, List<int> socialVenues,
        Dictionary<string, float> props, DateTime gameTime)
    {
        _sb.Clear();
        _sb.Append("{\"t\":\"").Append(gameTime.ToString("yyyy-MM-ddTHH:mm:ss")).Append('"');
        _sb.Append(",\"day\":0");
        _sb.Append(",\"npc\":").Append(npcId);
        _sb.Append(",\"evt\":\"npc_created\"");
        _sb.Append(",\"seed\":").Append(seed);
        _sb.Append(",\"role\":\"").Append(role).Append('"');
        _sb.Append(",\"home\":").Append(homeLocationId);
        _sb.Append(",\"workplace\":").Append(workplaceLocationId);
        _sb.Append(",\"social_venues\":").Append(JsonSerializer.Serialize(socialVenues, JsonOpts));
        _sb.Append(",\"props\":").Append(SerializeProps(props));
        _sb.Append(",\"schedule_template\":\"").Append(role).Append('"');
        _sb.Append('}');
        _writer.WriteLine(_sb);
    }


    public void LogNodeArrival(int npcId, string storylet, int locationId, string locationType,
        DateTime gameTime, int day, Dictionary<string, float> props,
        Dictionary<string, float> deltas)
    {
        _sb.Clear();
        _sb.Append("{\"t\":\"").Append(gameTime.ToString("yyyy-MM-ddTHH:mm:ss")).Append('"');
        _sb.Append(",\"day\":").Append(day);
        _sb.Append(",\"npc\":").Append(npcId);
        _sb.Append(",\"evt\":\"node_arrival\"");
        _sb.Append(",\"storylet\":\"").Append(storylet).Append('"');
        _sb.Append(",\"location\":").Append(locationId);
        _sb.Append(",\"location_type\":\"").Append(locationType).Append('"');
        _sb.Append(",\"props\":").Append(SerializeProps(props));
        _sb.Append(",\"post\":").Append(SerializeProps(deltas));
        _sb.Append('}');
        _writer.WriteLine(_sb);
    }


    public void LogEncounter(int npcA, int npcB, string interactionType, int locationId,
        string locationType, DateTime gameTime, int day,
        float trustBefore, float trustAfter)
    {
        _sb.Clear();
        _sb.Append("{\"t\":\"").Append(gameTime.ToString("yyyy-MM-ddTHH:mm:ss")).Append('"');
        _sb.Append(",\"day\":").Append(day);
        _sb.Append(",\"npc\":").Append(npcA);
        _sb.Append(",\"evt\":\"encounter\"");
        _sb.Append(",\"other\":").Append(npcB);
        _sb.Append(",\"interaction\":\"").Append(interactionType).Append('"');
        _sb.Append(",\"location\":").Append(locationId);
        _sb.Append(",\"location_type\":\"").Append(locationType).Append('"');
        _sb.Append(",\"trust_before\":").AppendFormat("{0:F3}", trustBefore);
        _sb.Append(",\"trust_after\":").AppendFormat("{0:F3}", trustAfter);
        _sb.Append('}');
        _writer.WriteLine(_sb);
    }


    public void LogRelationshipChanged(int npcA, int npcB, string oldTier, string newTier,
        float trust, int interactionCount, DateTime gameTime, int day)
    {
        _sb.Clear();
        _sb.Append("{\"t\":\"").Append(gameTime.ToString("yyyy-MM-ddTHH:mm:ss")).Append('"');
        _sb.Append(",\"day\":").Append(day);
        _sb.Append(",\"npc\":").Append(npcA);
        _sb.Append(",\"evt\":\"relationship_changed\"");
        _sb.Append(",\"other\":").Append(npcB);
        _sb.Append(",\"old_tier\":\"").Append(oldTier).Append('"');
        _sb.Append(",\"new_tier\":\"").Append(newTier).Append('"');
        _sb.Append(",\"trust\":").AppendFormat("{0:F3}", trust);
        _sb.Append(",\"interaction_count\":").Append(interactionCount);
        _sb.Append('}');
        _writer.WriteLine(_sb);
    }


    public void LogDaySummary(int npcId, int day, int storyletsCompleted, int encounters,
        Dictionary<string, float> props, Dictionary<int, float> topRelationships)
    {
        _sb.Clear();
        _sb.Append("{\"t\":\"day_end\"");
        _sb.Append(",\"day\":").Append(day);
        _sb.Append(",\"npc\":").Append(npcId);
        _sb.Append(",\"evt\":\"day_summary\"");
        _sb.Append(",\"storylets_completed\":").Append(storyletsCompleted);
        _sb.Append(",\"storylets_interrupted\":0");
        _sb.Append(",\"encounters\":").Append(encounters);
        _sb.Append(",\"props\":").Append(SerializeProps(props));
        _sb.Append(",\"relationships\":{");
        bool first = true;
        foreach (var (otherId, trust) in topRelationships)
        {
            if (!first) _sb.Append(',');
            _sb.Append('"').Append(otherId).Append("\":").AppendFormat("{0:F3}", trust);
            first = false;
        }
        _sb.Append("}}");
        _writer.WriteLine(_sb);
    }


    public void LogRequestEmitted(int requestId, int requesterId, string requestType, int locationId,
        float urgency, int timeoutMinutes, string storyletContext, DateTime gameTime, int day)
    {
        _sb.Clear();
        _sb.Append("{\"t\":\"").Append(gameTime.ToString("yyyy-MM-ddTHH:mm:ss")).Append('"');
        _sb.Append(",\"day\":").Append(day);
        _sb.Append(",\"npc\":").Append(requesterId);
        _sb.Append(",\"evt\":\"request_emitted\"");
        _sb.Append(",\"request_id\":").Append(requestId);
        _sb.Append(",\"request_type\":\"").Append(requestType).Append('"');
        _sb.Append(",\"location\":").Append(locationId);
        _sb.Append(",\"urgency\":").AppendFormat("{0:F2}", urgency);
        _sb.Append(",\"timeout_minutes\":").Append(timeoutMinutes);
        _sb.Append(",\"storylet_context\":\"").Append(storyletContext).Append('"');
        _sb.Append('}');
        _writer.WriteLine(_sb);
    }


    public void LogRequestClaimed(int requestId, int claimerId, DateTime gameTime, int day)
    {
        _sb.Clear();
        _sb.Append("{\"t\":\"").Append(gameTime.ToString("yyyy-MM-ddTHH:mm:ss")).Append('"');
        _sb.Append(",\"day\":").Append(day);
        _sb.Append(",\"npc\":").Append(claimerId);
        _sb.Append(",\"evt\":\"request_claimed\"");
        _sb.Append(",\"request_id\":").Append(requestId);
        _sb.Append('}');
        _writer.WriteLine(_sb);
    }


    public void LogSignalEmitted(int signalId, int requestId, string signalType, int sourceNpcId,
        DateTime gameTime, int day)
    {
        _sb.Clear();
        _sb.Append("{\"t\":\"").Append(gameTime.ToString("yyyy-MM-ddTHH:mm:ss")).Append('"');
        _sb.Append(",\"day\":").Append(day);
        _sb.Append(",\"npc\":").Append(sourceNpcId);
        _sb.Append(",\"evt\":\"signal_emitted\"");
        _sb.Append(",\"signal_id\":").Append(signalId);
        _sb.Append(",\"request_id\":").Append(requestId);
        _sb.Append(",\"signal_type\":\"").Append(signalType).Append('"');
        _sb.Append('}');
        _writer.WriteLine(_sb);
    }


    public void Flush()
    {
        _writer.Flush();
    }


    public void Dispose()
    {
        _writer.Flush();
        _writer.Dispose();
    }


    private static string SerializeProps(Dictionary<string, float> props)
    {
        if (props == null || props.Count == 0) return "{}";
        var sb = new StringBuilder(props.Count * 20);
        sb.Append('{');
        bool first = true;
        foreach (var (key, value) in props)
        {
            if (!first) sb.Append(',');
            sb.Append('"').Append(key).Append("\":").AppendFormat("{0:F3}", value);
            first = false;
        }
        sb.Append('}');
        return sb.ToString();
    }
}
