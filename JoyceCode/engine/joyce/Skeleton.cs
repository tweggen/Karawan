using System.Collections.Generic;

namespace engine.joyce;

public class Skeleton
{
    private SortedDictionary<string, Bone> _mapBones = new ();
    private uint _nextBoneIndex = 0;
    
    public uint NBones
    {
        get => _nextBoneIndex;
    }
    
    public Bone FindBone(string name)
    {
        Bone bone;
        if (!_mapBones.TryGetValue(name, out bone))
        {
            bone = new() { Index = _nextBoneIndex++ };
            _mapBones.Add(name, bone);
        }

        return bone;
    }
    
    /**
     * Add a weighted association to a bone to a given vertex.
     */
    public void AddWeight(engine.joyce.Mesh jMesh, uint vertexIndex, engine.joyce.Bone bone, float weight)
    {
        bone.AddWeight(jMesh, vertexIndex, weight);
    }
}