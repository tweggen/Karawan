using System;
using System.Collections.Generic;

namespace engine.tale;

/// <summary>
/// How an interrupt treats the storylet it interrupts. An encounter interrupt
/// is Nest or Replace (InterruptPriority >= 8 ? Replace : Nest). A claim uses
/// its trigger's "interrupt_scope" (KAR-477), Nest by default; no shipped
/// trigger declares one yet, so every claim nests. Cancel falls through to
/// normal selection and is kept so a trigger can declare it (S2b plan, §5).
/// </summary>
public enum InterruptScope { Nest, Replace, Cancel }

/// <summary>
/// Represents a paused storylet that can be resumed after an interrupt completes.
/// </summary>
public struct PausedStorylet
{
    public string StoryletId;
    public float RemainingDurationMinutes;
    public Dictionary<string, float> PropertiesAtPause;
}

/// <summary>
/// Manages the interrupt stack for an NPC: pending interrupts, paused arcs, and resumption logic.
/// </summary>
public class ArcStack
{
    public Stack<PausedStorylet> PausedArcs = new();
    public string? PendingInterruptStorylet;
    public InterruptScope? PendingInterruptScope;

    public bool HasPendingInterrupt => PendingInterruptStorylet != null;

    /** When the pending interrupt was set (game time); the live background tick steps it then (S2b-d). */
    public DateTime PendingInterruptAt;

    public void SetInterrupt(string storyletId, InterruptScope scope, DateTime at = default)
    {
        PendingInterruptStorylet = storyletId;
        PendingInterruptScope = scope;
        PendingInterruptAt = at;
    }

    public void ClearInterrupt()
    {
        PendingInterruptStorylet = null;
        PendingInterruptScope = null;
        PendingInterruptAt = default;
    }

    public void Push(PausedStorylet s) => PausedArcs.Push(s);

    public PausedStorylet? TryPop() => PausedArcs.Count > 0 ? PausedArcs.Pop() : null;
}
