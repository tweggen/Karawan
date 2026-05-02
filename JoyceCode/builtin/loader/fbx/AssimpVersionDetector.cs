using System;
using System.Linq;
using System.Reflection;
using engine.joyce;
using static engine.Logger;

namespace builtin.loader.fbx;

/// <summary>
/// Detects which version of Assimp is loaded at runtime.
/// Uses reflection to check Silk.NET.Assimp assembly version.
/// </summary>
public static class AssimpVersionDetector
{
    private static readonly engine.Dc _dc = engine.Dc.AssetLoading;
    private static AssimpVersion? _cachedNativeVersion = null;
    private static readonly object _lock = new();

    /// <summary>
    /// Set the native Assimp version by querying the actual loaded binary.
    /// Call this after Assimp is loaded in FbxModel._needAssimp().
    /// Thread-safe; subsequent calls are ignored.
    /// </summary>
    public static void SetNativeVersion(Silk.NET.Assimp.Assimp assimp)
    {
        if (_cachedNativeVersion.HasValue)
            return;

        lock (_lock)
        {
            if (_cachedNativeVersion.HasValue)
                return;

            try
            {
                uint major = assimp.GetVersionMajor();
                uint minor = assimp.GetVersionMinor();

                if (major == 5)
                {
                    _cachedNativeVersion = AssimpVersion.Assimp5_4_1;
                    Trace(_dc, $"Detected native Assimp: 5.4.1");
                }
                else if (major == 6)
                {
                    _cachedNativeVersion = AssimpVersion.Assimp6_0_2;
                    Trace(_dc, $"Detected native Assimp: 6.0.2");
                }
                else
                {
                    Error(_dc, $"Unknown native Assimp major version: {major}, defaulting to 6.0.2");
                    _cachedNativeVersion = AssimpVersion.Assimp6_0_2;
                }
            }
            catch (Exception e)
            {
                Error(_dc, $"Exception while detecting native Assimp version: {e}");
                _cachedNativeVersion = AssimpVersion.Assimp6_0_2;
            }
        }
    }

    /// <summary>
    /// Get the Assimp version currently loaded (native binary version).
    /// Must be called after SetNativeVersion() has been called from FbxModel._needAssimp().
    /// </summary>
    public static AssimpVersion GetVersion()
    {
        if (_cachedNativeVersion.HasValue)
        {
            return _cachedNativeVersion.Value;
        }

        Warning(_dc, "GetVersion() called before SetNativeVersion() - Assimp not yet loaded?");
        return AssimpVersion.Assimp6_0_2;
    }

    /// <summary>
    /// Check if we're using a specific Assimp version.
    /// Convenience method for version checks in compensation code.
    /// </summary>
    public static bool IsVersion(AssimpVersion version)
    {
        return GetVersion() == version;
    }

    /// <summary>
    /// Check if we're using Assimp 5.4.1 or older.
    /// </summary>
    public static bool IsAssimp5OrOlder()
    {
        return GetVersion() <= AssimpVersion.Assimp5_4_1;
    }

    /// <summary>
    /// Check if we're using Assimp 6.0 or newer.
    /// </summary>
    public static bool IsAssimp6OrNewer()
    {
        return GetVersion() >= AssimpVersion.Assimp6_0_2;
    }

    /// <summary>
    /// Get the native Assimp library version (from the actual binary loaded).
    /// This may differ from the Silk.NET wrapper version if different binaries are shipped per-platform.
    /// </summary>
    public static unsafe string GetNativeAssimpVersion(Silk.NET.Assimp.Assimp assimp)
    {
        try
        {
            uint major = assimp.GetVersionMajor();
            uint minor = assimp.GetVersionMinor();
            uint patch = assimp.GetVersionPatch();
            return $"{major}.{minor}.{patch}";
        }
        catch (Exception e)
        {
            Warning(_dc, $"Exception while getting native Assimp version: {e}");
            return "unknown";
        }
    }
}
