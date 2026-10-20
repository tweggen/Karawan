namespace engine.tale;

/// <summary>
/// The words narration scripts use for an NPC's state (Phase C3,
/// func.npcMood() and func.npcWealthLabel()). Pure, so the thresholds are
/// tested (KAR-433).
/// </summary>
public static class NpcMoodText
{
    /// <summary>frustrated (anger &gt; 0.7), desperate (hunger &gt; 0.7 or wealth &lt; 0.2), tired (fatigue &gt; 0.7), else neutral; in that order.</summary>
    public static string Mood(float anger, float hunger, float fatigue, float wealth)
    {
        if (anger > 0.7f) return "frustrated";
        if (hunger > 0.7f || wealth < 0.2f) return "desperate";
        if (fatigue > 0.7f) return "tired";
        return "neutral";
    }


    /// <summary>broke (&lt; 0.15), struggling (&lt; 0.35), comfortable (≤ 0.65), else wealthy.</summary>
    public static string WealthLabel(float wealth)
    {
        if (wealth < 0.15f) return "broke";
        if (wealth < 0.35f) return "struggling";
        if (wealth <= 0.65f) return "comfortable";
        return "wealthy";
    }
}
