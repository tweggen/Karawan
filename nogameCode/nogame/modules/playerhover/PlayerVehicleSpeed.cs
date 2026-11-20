namespace nogame.modules.playerhover;

/// <summary>
/// TALE S1-d: the player ship's speed over the last two logical frames,
/// written by HoverModule. A contact callback arrives after the physics step,
/// when the ship may already have been slowed by the impact, so a run-over is
/// judged by the higher of the two. Logical thread only.
/// </summary>
public static class PlayerVehicleSpeed
{
    private static float _previous;
    private static float _current;

    /// <summary>Metres per second, the higher of the last two frames; 0 when not in the ship.</summary>
    public static float Recent => _previous > _current ? _previous : _current;

    public static void Record(float speed)
    {
        _previous = _current;
        _current = speed;
    }

    /// <summary>
    /// KAR-483: how much the intercity corridor currently boosts the ship (1 = none);
    /// set by HoverController, read by the speed display.
    /// </summary>
    public static float Boost { get; set; } = 1f;

    public static void Reset()
    {
        _previous = 0f;
        _current = 0f;
        Boost = 1f;
    }
}
