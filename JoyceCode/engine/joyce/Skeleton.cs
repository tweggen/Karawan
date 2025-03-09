using System.Collections.Generic;

namespace engine.joyce;

public class Skeleton
{
    private SortedDictionary<string, Bone> _mapBones = new ();
    private List<Bone> _listBones = new();
    
    private uint _nextBoneIndex = 0;
    
    
    public IList<Bone> ListBones
    {
        get => _listBones;
    }
    
    
    public IDictionary<string, Bone> MapBones
    {
        get => _mapBones;
    }
    
    
    public uint NBones
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
    
    #if false
    /**
     * Add a weighted association to a bone to a given vertex.
     */
    public void AddWeight(engine.joyce.Mesh jMesh, uint vertexIndex, engine.joyce.Bone bone, float weight)
    {
        bone.AddWeight(jMesh, vertexIndex, weight);
    }
    #endif
}