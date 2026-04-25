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
    private static AssimpVersion? _cachedVersion = null;

    /// <summary>
    /// Get the Assimp version currently loaded.
    /// Result is cached after first call.
    /// </summary>
    public static AssimpVersion GetVersion()
    {
        if (_cachedVersion.HasValue)
        {
            return _cachedVersion.Value;
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
                _cachedVersion = AssimpVersion.Assimp6_0_2;
                return _cachedVersion.Value;
            }

            var version = silkAssimpAssembly.GetName().Version;
            Trace(_dc, $"Detected Silk.NET.Assimp version: {version}");

            // Map Silk.NET versions to Assimp versions
            // Silk.NET 2.22.0 -> Assimp 5.4.1
            // Silk.NET 2.23.0 -> Assimp 6.0.2
            if (version.Major == 2)
            {
                if (version.Minor <= 22)
                {
                    Trace(_dc, $"Mapped to Assimp 5.4.1");
                    _cachedVersion = AssimpVersion.Assimp5_4_1;
                }
                else
                {
                    Trace(_dc, $"Mapped to Assimp 6.0.2");
                    _cachedVersion = AssimpVersion.Assimp6_0_2;
                }
            }
            else
            {
                // Unknown version, default to 6.0.2
                Warning(_dc, $"Unknown Silk.NET version {version}, defaulting to Assimp6_0_2");
                _cachedVersion = AssimpVersion.Assimp6_0_2;
            }
        }
        catch (Exception e)
        {
            Warning(_dc, $"Exception while detecting Assimp version: {e}");
            _cachedVersion = AssimpVersion.Assimp6_0_2;
        }

        return _cachedVersion.Value;
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
}
