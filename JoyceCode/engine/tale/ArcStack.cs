using System;
using System.Collections.Generic;

namespace engine.tale;

/// <summary>
/// How an interrupt treats the storylet it interrupts. DES produces only Nest
/// and Replace (InterruptPriority >= 8 ? Replace : Nest). Cancel is handled
/// (DesSimulation falls through to normal selection) but never produced, and is
/// kept on purpose: S2b, which ports interrupts to the live game, decides how
/// storylets declare their scope (KAR-436). The claim trigger's parsed
/// "interrupt_scope" is unused for the same reason.
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

    public void SetInterrupt(string storyletId, InterruptScope scope)
    {
        PendingInterruptStorylet = storyletId;
        PendingInterruptScope = scope;
    }

    public void ClearInterrupt()
    {
        PendingInterruptStorylet = null;
        PendingInterruptScope = null;
    }

    public void Push(PausedStorylet s) => PausedArcs.Push(s);

    public PausedStorylet? TryPop() => PausedArcs.Count > 0 ? PausedArcs.Pop() : null;
}
