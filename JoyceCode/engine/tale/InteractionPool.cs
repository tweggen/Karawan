using System;
using System.Collections.Generic;
using System.Linq;

namespace engine.tale;

/// <summary>
/// Cluster-scoped pool of interaction requests and signals.
/// NPCs emit requests, other NPCs claim and fulfill them, signals flow back.
/// </summary>
public class InteractionPool
{
    private readonly Dictionary<int, InteractionRequest> _requests = new();
    private readonly Dictionary<int, InteractionSignal> _signals = new();
    private int _nextRequestId = 1;
    private int _nextSignalId = 1;

    public IReadOnlyDictionary<int, InteractionRequest> Requests => _requests;
    public IReadOnlyDictionary<int, InteractionSignal> Signals => _signals;


    /// <summary>
    /// Emit a new request to the pool.
    /// Assigns an ID and tracks it.
    /// </summary>
    public int EmitRequest(int requesterId, string type, int locationId, float urgency,
        DateTime timeout, string storyletContext)
    {
        int requestId = _nextRequestId++;
        var request = new InteractionRequest
        {
            Id = requestId,
            RequesterId = requesterId,
            Type = type,
            LocationId = locationId,
            Urgency = urgency,
            Timeout = timeout,
            StoryletContext = storyletContext,
            ClaimerId = null,
            EmittedAt = timeout - TimeSpan.FromMinutes(1) // Assume emitted just now
        };
        _requests[requestId] = request;
        return requestId;
    }


    /// <summary>
    /// Find an unclaimed request matching the given type.
    /// Optionally filters by role and location proximity.
    /// </summary>
    public InteractionRequest FindMatchingRequest(string requestType, string? roleFilter = null,
        int? locationIdFilter = null, float? maxDistanceNormalized = null)
    {
        var candidates = _requests.Values
            .Where(r => r.Type == requestType && !r.ClaimerId.HasValue && r.Timeout > DateTime.UtcNow)
            .ToList();

        if (!candidates.Any())
            return null;

        // For now, return highest urgency unclaimed request
        // Could be extended to include location/role filtering
        return candidates.OrderByDescending(r => r.Urgency).First();
    }


    /// <summary>
    /// Claim a request for a specific NPC.
    /// Returns true if claim succeeded, false if already claimed.
    /// </summary>
    public bool ClaimRequest(int requestId, int claimerId)
    {
        if (!_requests.TryGetValue(requestId, out var request))
            return false;

        if (request.ClaimerId.HasValue)
            return false; // Already claimed

        request.ClaimerId = claimerId;
        return true;
    }


    /// <summary>
    /// Emit a signal in response to a request.
    /// </summary>
    public int EmitSignal(int requestId, string signalType, int sourceNpcId, DateTime emittedAt)
    {
        int signalId = _nextSignalId++;
        var signal = new InteractionSignal
        {
            Id = signalId,
            RequestId = requestId,
            SignalType = signalType,
            SourceNpcId = sourceNpcId,
            EmittedAt = emittedAt
        };
        _signals[signalId] = signal;
        return signalId;
    }


    /// <summary>
    /// Check if a request has a signal of a specific type.
    /// </summary>
    public bool HasSignal(int requestId, string signalType)
    {
        return _signals.Values.Any(s => s.RequestId == requestId && s.SignalType == signalType);
    }


    /// <summary>
    /// Get all signals for a request.
    /// </summary>
    public IReadOnlyList<InteractionSignal> GetSignalsForRequest(int requestId)
    {
        return _signals.Values
            .Where(s => s.RequestId == requestId)
            .ToList();
    }


    /// <summary>
    /// Remove expired requests (past timeout).
    /// Called during daily cleanup.
    /// </summary>
    public int PurgeExpired(DateTime now)
    {
        var expiredIds = _requests
            .Where(kvp => kvp.Value.Timeout <= now)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (int id in expiredIds)
            _requests.Remove(id);

        return expiredIds.Count;
    }


    /// <summary>
    /// Get all active (unclaimed, non-expired) requests.
    /// </summary>
    public IReadOnlyList<InteractionRequest> GetActiveRequests(DateTime now = default)
    {
        if (now == default)
            now = DateTime.UtcNow;

        return _requests.Values
            .Where(r => !r.ClaimerId.HasValue && r.Timeout > now)
            .ToList();
    }


    /// <summary>
    /// Get all claimed but not yet fulfilled requests.
    /// </summary>
    public IReadOnlyList<InteractionRequest> GetPendingRequests(DateTime now = default)
    {
        if (now == default)
            now = DateTime.UtcNow;

        return _requests.Values
            .Where(r => r.ClaimerId.HasValue && r.Timeout > now &&
                !_signals.Values.Any(s => s.RequestId == r.Id && s.SignalType == "request_fulfilled"))
            .ToList();
    }


    /// <summary>
    /// Get request count metrics.
    /// </summary>
    public (int Active, int Claimed, int Expired, int Fulfilled) GetMetrics(DateTime now = default)
    {
        if (now == default)
            now = DateTime.UtcNow;

        int active = GetActiveRequests(now).Count;
        int claimed = _requests.Values.Count(r => r.ClaimerId.HasValue && r.Timeout > now);
        int expired = _requests.Values.Count(r => r.Timeout <= now);
        int fulfilled = _signals.Values.Count(s => s.SignalType == "request_fulfilled");

        return (active, claimed, expired, fulfilled);
    }


    /// <summary>
    /// Clear all requests and signals (for testing or new simulation).
    /// </summary>
    public void Clear()
    {
        _requests.Clear();
        _signals.Clear();
        _nextRequestId = 1;
        _nextSignalId = 1;
    }
}
