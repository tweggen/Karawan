using System;
using System.Security.Cryptography;
using System.Text;

namespace engine.tale.bake;

/// <summary>
/// Hash a scenario identity (category name, index, seed) into a deterministic
/// "sc-{base64hash}" filename. Mirrors the "ac-{hash}" naming scheme used for
/// baked animation collections, so Chushi (build-time bake), GameConfig (resource
/// manifest emission) and the runtime asset loader all agree on file names.
///
/// NOTE: this helper is intentionally duplicated in Tooling/Cmdline/GameConfig.cs
/// because the Cmdline tooling project does not reference JoyceCode. The animation
/// pipeline uses the same convention (compare ModelAnimationCollectionFileName).
/// </summary>
public static class ScenarioFileName
{
    private static readonly SHA256 _sha256 = SHA256.Create();

    /// <summary>
    /// Build the canonical bake key for a scenario. The seed is part of the key
    /// so re-tuning a category's base seed naturally invalidates its bake artifact.
    /// </summary>
    public static string BuildKey(string categoryName, int index, int seed)
    {
        return $"{categoryName};{index};{seed}";
    }

    /// <summary>
    /// Compute the bake filename for a scenario identity.
    /// </summary>
    public static string Of(string categoryName, int index, int seed)
    {
        return OfKey(BuildKey(categoryName, index, seed));
    }

    public static string OfKey(string key)
    {
        byte[] hash;
        // SHA256 instances are not thread-safe.
        lock (_sha256)
        {
            hash = _sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
        }
        string strHash = Convert.ToBase64String(hash)
            .Replace('+', '-')
            .Replace('/', '_')
            .Replace('=', '~');
        return $"sc-{strHash}";
    }
}
