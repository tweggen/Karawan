using System.Collections.Generic;
using MessagePack;

namespace engine.joyce;

/**
 * The set of bones a model's meshes can be weighted against.
 *
 * WP-4.1 note on what is and is not persisted. _mapBones and _listBones hold the
 * SAME Bone instances - FindBone writes both - and MessagePack does not track
 * references: serialising both would write every bone twice and deserialise into
 * two disjoint sets, so a lookup by name would return a different object than the
 * lookup by index. Only the list is persisted, and the map is rebuilt from it in
 * OnAfterDeserialize, which makes the shared identity structural instead of
 * something the format has to preserve.
 *
 * The list is also the ordering that matters: Bone.Index is an index into
 * AllBakedMatrices, so list order == index order is the invariant AC-4.3 checks.
 */
[MessagePackObject(AllowPrivate = true)]
public partial class Skeleton : IMessagePackSerializationCallbackReceiver
{
    [IgnoreMember]
    private SortedDictionary<string, Bone> _mapBones = new ();

    [Key(0)]
    private List<Bone> _listBones = new();

    [Key(1)]
    private int _nextBoneIndex = 0;


    [IgnoreMember]
    public IList<Bone> ListBones
    {
        get => _listBones;
    }


    [IgnoreMember]
    public IDictionary<string, Bone> MapBones
    {
        get => _mapBones;
    }


    [IgnoreMember]
    public int NBones
    {
        get => _nextBoneIndex;
    }

    public Bone FindBone(string name)
    {
        Bone bone;
        if (!_mapBones.TryGetValue(name, out bone))
        {
            bone = new() { Index = _nextBoneIndex++, Name = name };
            _mapBones.Add(name, bone);
            _listBones.Add(bone);
        }

        return bone;
    }


    public void OnBeforeSerialize()
    {
    }


    /**
     * Rebuild the by-name index over the very same Bone objects the list holds.
     */
    public void OnAfterDeserialize()
    {
        _listBones ??= new();
        _mapBones = new();
        foreach (var bone in _listBones)
        {
            if (null == bone || null == bone.Name)
            {
                continue;
            }

            _mapBones[bone.Name] = bone;
        }
    }
}
