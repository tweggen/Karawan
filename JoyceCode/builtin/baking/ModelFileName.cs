using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using builtin.loader;
using static engine.Logger;

namespace builtin.baking;

/**
 * Naming for baked models (WP-4.2), the mo-{hash} counterpart to ac-{hash}.
 *
 * The hash covers the model url AND the load properties that change the geometry
 * the file contains, because two call sites loading the same fbx with different
 * properties do not get the same Model.
 *
 * Which properties those are is decided by EXCLUSION, deliberately. Only three
 * are known not to reach the persisted graph, and they are listed below; anything
 * else - including a property that does not exist yet - counts as significant and
 * changes the hash. That way a newly added property causes a re-bake, which is
 * merely wasteful, instead of silently reusing a file baked without it, which
 * would be wrong and invisible.
 *
 * WARNING: this scheme is duplicated in Tooling/Cmdline/GameConfig.ModelFileName
 * because that project cannot reference JoyceCode - the same arrangement as
 * ModelAnimationCollectionFileName and ScenarioFileName. CHANGE BOTH TOGETHER, or
 * the packaging manifests will list file names the bake never produces.
 */
public static class ModelFileName
{
    private static readonly engine.Dc _dc = engine.Dc.AssetLoading;

    private static readonly object _clo = new();
    private static readonly SHA256 _sha256 = SHA256.Create();

    /**
     * Load properties that provably do not affect the persisted model graph.
     *
     * - AnimationUrls: the additional fbx files are read for their animation
     *   CHANNELS only. FbxModel loads them with MergePolicy.LoadMeshes = false and
     *   _loadAnimations only ever reads ModelNodeTree.MapNodes, never merges into
     *   it or calls Skeleton.FindBone - so neither the node tree nor the skeleton
     *   depends on them. Their output is the ac-{hash} file, not this one.
     * - CPUNodes: selects which bones get a CPU-side baked frame array. Animation
     *   bake input only.
     * - ModelBaseBone: consumed by Model.Polish, which the loader re-runs after
     *   reading a baked model anyway.
     */
    private static readonly string[] _insignificantProperties =
    {
        "AnimationUrls",
        "CPUNodes",
        "ModelBaseBone",
    };


    /**
     * The exact string that gets hashed. Public so a test can pin it, and so the
     * Tooling/Cmdline copy has something unambiguous to mirror.
     */
    public static string KeyOf(string localUrlModel, IEnumerable<KeyValuePair<string, string>>? properties)
    {
        var sb = new StringBuilder(localUrlModel);

        if (null != properties)
        {
            /*
             * Sorted so the key does not depend on the order the caller happened to
             * add the properties in. ModelProperties is a SortedDictionary today;
             * sorting here means the hash does not silently depend on that.
             */
            foreach (var kvp in properties
                         .Where(kvp => !_insignificantProperties.Contains(kvp.Key))
                         .OrderBy(kvp => kvp.Key, StringComparer.Ordinal))
            {
                sb.Append(';').Append(kvp.Key).Append('=').Append(kvp.Value);
            }
        }

        return sb.ToString();
    }


    public static string Of(string localUrlModel, IEnumerable<KeyValuePair<string, string>>? properties)
    {
        string strKey = KeyOf(localUrlModel, properties);

        string strHash;
        lock (_clo)
        {
            strHash =
                Convert.ToBase64String(_sha256.ComputeHash(Encoding.UTF8.GetBytes(strKey)))
                    .Replace('+', '-')
                    .Replace('/', '_')
                    .Replace('=', '~');
        }

        Trace(_dc, $"Returning hash {strHash} for {strKey}");
        return $"mo-{strHash}";
    }


    public static string Of(string localUrlModel, ModelProperties? modelProperties)
        => Of(localUrlModel, modelProperties?.Properties);
}
