using System;
using System.Collections.Generic;
using System.Numerics;

namespace engine.joyce;

public class Joint
{
    /**
     * The node this joint controls
     */
    public IList<ModelNode> ControlledNodes;
    public Matrix4x4 InverseBindMatrix;
}


public class Skin
{
    public string Name;
    public IList<Joint> Joints;
}


/**
 * Represent a loaded or generated model.
 *
 * This contains
 * - general information about the model.
 * - a hierarchy of InstanceDescs.
 */
public class Model
{
    public string Name;

    public ModelNode RootNode;
    private int _nextNodeIndex = 1;
    private int _nextAnimIndex = 1;
    public Skeleton? Skeleton = null;
    public SortedDictionary<string, ModelAnimation> MapAnimations;
    public SortedDictionary<string, ModelNode> MapNodes = new();

    /**
     * Convenience method to create a model from a single InstanceDesc
     */
    public Model(InstanceDesc instanceDesc)
    {
        ModelNode mnRoot = new()
        {
            Model = this,
            InstanceDesc = instanceDesc,
            Transform = new(true, 0xffff, Matrix4x4.Identity)
        };
        RootNode = mnRoot;
    }


    public ModelNode CreateNode()
    {
        return new()
        {
            Model = this,
            Index = _nextNodeIndex++
        };
    }


    public ModelAnimation CreateAnimation()
    {
        return new ModelAnimation()
        {
            Index = _nextAnimIndex++
        };
    }


    /**
     * Bake all animations for the given node.
     */
    private void _bakeRecursive(ModelNode me, Matrix4x4 m4ParentTransform, ModelAnimation ma, uint frameno)
    {
        var skeleton = Skeleton!;
        
        /*
         * Find the appropriate bone.  
         */
        Bone? bone = null;
        uint boneIndex = 0; 
        Matrix4x4 m4Model2Bone;
        if (skeleton.MapBones.TryGetValue(me.Name, out bone))
        {
            m4Model2Bone = bone.Model2Bone;
            boneIndex = bone.Index;
        }
        else
        {
            m4Model2Bone = Matrix4x4.Identity;
        }
        
        Matrix4x4 m4Anim;
        if (ma.MapChannels.TryGetValue(me, out var mac))
        {
            /*
             * We do have an animation channel for this node.
             * So consider the animation below.
             *
             * Apply it to the matrix.
             */
            var kfPosition = mac.LerpPosition(frameno);
            var kfRotation = mac.SlerpRotation(frameno);
            var kfScaling = mac.LerpScaling(frameno); 
            mac.Positions[frameno] = kfPosition;
            mac.Rotations[frameno] = kfRotation;
            mac.Scalings[frameno] = kfScaling;
            m4Anim =
                Matrix4x4.CreateFromQuaternion(kfRotation.Value)
                * Matrix4x4.CreateScale(kfScaling.Value)
                * Matrix4x4.CreateTranslation(kfPosition.Value);
        }
        else
        {
            m4Anim = Matrix4x4.Identity;
        }
        
        /*
         * This is the complete transformation of this node,
         */
        Matrix4x4 m4MyTransform = m4ParentTransform * me.Transform.Matrix * m4Anim;
        
        /*
         * Store resulting matrix if we have a bone that carries it.
         * Otherwise, just pass it on to the children.
         */
        if (bone != null)
        {
            ma.BakedFrames[frameno].BoneTransformations[boneIndex] = m4Model2Bone * m4MyTransform;
        }


        if (me.Children != null)
        {
            /*
             * Now call ourselves recursively for each of our children
             */
            foreach (var child in me.Children)
            {
                _bakeRecursive(child, m4MyTransform, ma, frameno);
            }
        }
    }


    /**
     * Compute frame accurate interpolations for all bones for all animations.
     */
    public void BakeAnimations()
    {
        if (null == MapAnimations || null == Skeleton)
        {
            return;
        }

        var skeleton = FindSkeleton();

        /*
         * First, for all animations, create the arrays of matrices for
         * each bone per frame.,
         */
        foreach (var kvp in MapAnimations)
        {
            ModelAnimation ma = kvp.Value;

            /*
             * How many real frames does this animation have?
             */
            float duration = ma.Duration;
            uint nFrames = UInt32.Min((uint)(duration / 60f), 1);
            ma.NFrames = nFrames;
            ma.BakedFrames = new ModelBakedFrame[ma.NFrames];
            for (int frameno = 0; frameno < nFrames; ++frameno)
            {
                ModelBakedFrame bakedFrame = new()
                {
                    BoneTransformations = new Matrix4x4[Skeleton.NBones]
                };
                ma.BakedFrames[frameno] = bakedFrame;

                /*
                 * Now we have the space to compute the position of each and every bone.
                 */
                var nBones = skeleton.NBones;
            }

            /*
             * Now for this animation, for every frame, recurse throught the bones.
             */
            for (uint frameno = 0; frameno < ma.NFrames; ++frameno)
            {
                _bakeRecursive(RootNode, Matrix4x4.Identity, ma, frameno);

            }
        }
    }


    /**
     * Fill my model structure and my root instance desc with the
     * contents from the other model.
     */
    public void FillPlaceholderFrom(Model other)
    {
        /*
         * We will use their rootnode and their name, however use our InstanceDesc
         * as we already gave out our instanceDesc to clients.
         */
        Name = other.Name;
        _nextNodeIndex = other._nextNodeIndex;
        _nextAnimIndex = other._nextAnimIndex;
        Skeleton = other.Skeleton;
        MapAnimations = other.MapAnimations;
        MapNodes = other.MapNodes;
        RootNode = other.RootNode;
        Name = other.Name;
    }


    public Skeleton FindSkeleton()
    {
        if (null == Skeleton)
        {
            Skeleton = new();
        }

        return Skeleton;
    }
    

    public Model()
    {
    }
}
