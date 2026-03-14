using System;

namespace engine.tale;

/// <summary>
/// Represents a request emitted by one NPC that another can claim and fulfill.
/// Examples: food_delivery, help, trade_service, etc.
/// </summary>
public class InteractionRequest
{
    public int Id;                          // Unique request ID (auto-assigned by pool)
    public int RequesterId;                 // NPC who emitted this request
    public string Type;                     // "food_delivery", "help", "trade", etc.
    public int LocationId;                  // Where the request applies (requester's location)
    public float Urgency;                   // 0.0-1.0, affects claimer priority
    public DateTime Timeout;                // When request expires
    public string StoryletContext;          // Which storylet emitted this request
    public int? ClaimerId;                  // NPC who claimed (null until claimed)
    public DateTime EmittedAt;              // When request was emitted
}
