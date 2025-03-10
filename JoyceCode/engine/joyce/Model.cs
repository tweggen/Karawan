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
    private void _bakeRecursive(ModelNode me, Matrix4x4 m4ParentTransform)
    {
        /*
         * we have the absolute matrix of the parent
         */
        Matrix4x4 m4MyTransform = m4ParentTransform * me.Transform.Matrix;
        
        /*
         * Do we have a bone with animation data associated?
         */
        foreach (var kvpMa in MapAnimations)
        {
            ModelAnimation ma = kvpMa.Value;

            ModelAnimChannel? mac; 
            if (ma.MapChannels.TryGetValue(me, out mac))
            {
                /*
                 * We do have an animation channel for this node.
                 * So consider the animation below.
                 */
            }
            else
            {
                mac = null;
            }
            
            
            /*
             * Now we have room 
             */
            if (null != mac)
            {
                /*
                * Apply the animation to this frame
                */
            }
            else
            {
                /*
                * Store a constant value for this node for all frames.
                */
            }

            if (me.Children != null)
            {
                /*
                 * Now call ourselves recursively for each of our children
                 * recursively
                 */
                foreach (var child in me.Children)
                {
                    _bakeRecursive(child, m4MyTransform);
                }
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
                
                /*
                 * Now we have the space to compute the position of each and every bone.
                 */
                var nBones = skeleton.NBones;
            }
        }
        
        // TXWTODO: Check the matrix
        /*
         * We hvae the containers, fill them with data for all nodes, applying animations
         * along the way.
         */
        _bakeRecursive(RootNode, Matrix4x4.Identity);
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
