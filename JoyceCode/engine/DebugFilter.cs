using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace engine;

/// <summary>
/// Fast, runtime-configurable debug output category filter.
///
/// Usage:
///   private static readonly Dc _dc = Dc.Pathfinding;
///   Trace(_dc, $"A* exploring {j.Position}");
///
/// Configuration key pattern in nogame.globalSettings.json:
///   "debug.category.pathfinding": "true"
///
/// Thread safety:
///   Reads are plain array loads (benign races acceptable — worst case is one stale frame).
///   Writes via SetCategory() use Volatile.Write to flush the store buffer.
/// </summary>
public static class DebugFilter
{
    // The single backing array. Plain bool[] — reads are atomic by ECMA spec §I.12.6.6.
    private static readonly bool[] _enabled = new bool[(int)Dc._Count];

    /// <summary>
    /// Hot-path check. Inlined by JIT. Single array-bounds-check + load.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Is(Dc category) => _enabled[(int)category];

    /// <summary>
    /// Set a category at runtime. Uses Volatile.Write to ensure cross-thread visibility.
    /// Call from console commands, tests, or the GlobalSettings loader.
    /// </summary>
    public static void SetCategory(Dc category, bool enabled)
        => Volatile.Write(ref _enabled[(int)category], enabled);

    /// <summary>
    /// Scan the GlobalSettings dictionary and apply all "debug.category.*" entries.
    /// Called once from GlobalSettings._whenLoaded() or from DebugFilter.StartLoading().
    /// </summary>
    public static void ApplyFromSettings()
    {
        // Iterate all known categories by name to avoid a dependency on
        // GlobalSettings.Dictionary implementation details.
        var names = Enum.GetNames<Dc>();
        foreach (var name in names)
        {
            if (name == "_Count") continue;

            // Key convention: "debug.category.pathfinding" (lowercase name)
            string key = $"debug.category.{name.ToLowerInvariant()}";
            string val = GlobalSettings.Get(key);
            if (!string.IsNullOrEmpty(val))
            {
                if (Enum.TryParse<Dc>(name, out var dc))
                {
                    bool enabled = val.Equals("true", StringComparison.OrdinalIgnoreCase);
                    Volatile.Write(ref _enabled[(int)dc], enabled);
                }
            }
        }
    }
}
