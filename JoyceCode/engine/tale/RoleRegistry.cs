using System;
using System.Collections.Generic;

namespace engine.tale;

/// <summary>
/// Central registry of all roles available in the game.
/// Inherits thread-safe storage from ObjectRegistry<RoleDefinition>.
/// Populated from game configuration during engine load via Loader.WhenLoaded.
/// </summary>
public class RoleRegistry : ObjectRegistry<RoleDefinition>
{
    private float[] _normalizedWeights;

    /// <summary>Get normalized weights for role distribution (0.0–1.0, sum = 1.0).</summary>
    public float[] GetNormalizedWeights()
    {
        if (_normalizedWeights == null)
        {
            var roleIds = GetKeys(); // Thread-safe key retrieval from ObjectRegistry
            float total = 0;
            foreach (var roleId in roleIds)
            {
                var role = Get(roleId);
                if (role != null)
                    total += role.DefaultWeight;
            }

            _normalizedWeights = new float[roleIds.Count];
            for (int i = 0; i < roleIds.Count; i++)
            {
                var role = Get(roleIds[i]);
                _normalizedWeights[i] = (role?.DefaultWeight ?? 0) / (total > 0 ? total : 1);
            }
        }
        return _normalizedWeights;
    }

    /// <summary>Pick a role deterministically from weights.</summary>
    public string PickRoleFromWeights(Random rng)
    {
        var roleIds = GetKeys();
        float[] weights = GetNormalizedWeights();
        float roll = (float)rng.NextDouble();
        float cumulative = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (roll < cumulative)
                return roleIds[i];
        }
        return roleIds.Count > 0 ? roleIds[0] : "worker";
    }
}
