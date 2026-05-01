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
    private static AssimpVersion? _cachedWrapperVersion = null;
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
                    Warning(_dc, $"Unknown native Assimp major version: {major}, defaulting to 6.0.2");
                    _cachedNativeVersion = AssimpVersion.Assimp6_0_2;
                }
            }
            catch (Exception e)
            {
                Warning(_dc, $"Exception while detecting native Assimp version: {e}");
                _cachedNativeVersion = AssimpVersion.Assimp6_0_2;
            }
        }
    }

    /// <summary>
    /// Get the Assimp version currently loaded (native binary, or wrapper if native not yet detected).
    /// Result is cached after first call.
    /// Thread-safe; only one thread performs detection.
    /// </summary>
    public static AssimpVersion GetVersion()
    {
        if (_cachedNativeVersion.HasValue)
        {
            return _cachedNativeVersion.Value;
        }

        if (_cachedWrapperVersion.HasValue)
        {
            return _cachedWrapperVersion.Value;
        }

        lock (_lock)
        {
            // Double-check after acquiring lock
            if (_cachedNativeVersion.HasValue)
            {
                return _cachedNativeVersion.Value;
            }

            if (_cachedWrapperVersion.HasValue)
            {
                return _cachedWrapperVersion.Value;
            }

            try
            {
                // Try to find Silk.NET.Assimp assembly
                var silkAssimpAssembly = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Silk.NET.Assimp");

                if (silkAssimpAssembly == null)
                {
                    Warning(_dc, "Silk.NET.Assimp assembly not found, defaulting to Assimp6_0_2");
                    _cachedWrapperVersion = AssimpVersion.Assimp6_0_2;
                    return _cachedWrapperVersion.Value;
                }

                var version = silkAssimpAssembly.GetName().Version;
                Trace(_dc, $"Detected Silk.NET.Assimp wrapper version: {version}");

                // Map Silk.NET versions to Assimp versions
                // Silk.NET 2.22.0 -> Assimp 5.4.1
                // Silk.NET 2.23.0 -> Assimp 6.0.2
                if (version.Major == 2)
                {
                    if (version.Minor <= 22)
                    {
                        Trace(_dc, $"Wrapper mapped to Assimp 5.4.1");
                        _cachedWrapperVersion = AssimpVersion.Assimp5_4_1;
                    }
                    else
                    {
                        Trace(_dc, $"Wrapper mapped to Assimp 6.0.2");
                        _cachedWrapperVersion = AssimpVersion.Assimp6_0_2;
                    }
                }
                else
                {
                    // Unknown version, default to 6.0.2
                    Warning(_dc, $"Unknown Silk.NET wrapper version {version}, defaulting to Assimp6_0_2");
                    _cachedWrapperVersion = AssimpVersion.Assimp6_0_2;
                }
            }
            catch (Exception e)
            {
                Warning(_dc, $"Exception while detecting wrapper Assimp version: {e}");
                _cachedWrapperVersion = AssimpVersion.Assimp6_0_2;
            }

            return _cachedWrapperVersion.Value;
        }
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
