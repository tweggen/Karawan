using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using builtin.extensions;
using static engine.Logger;

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

    public uint MAX_BONES = 60;
    
    private ModelNode _mnRoot; 
    public ModelNode RootNode
    {
        get => _mnRoot;
        set
        {
            _mnRoot = value;
        } 
    }
    private int _nextNodeIndex = 1;
    private int _nextAnimIndex = 1;
    private uint _nextAnimFrame = 0;

    public void PushAnimFrames(uint nFrames)
    {
        _nextAnimFrame += nFrames;
    }
    
    public Skeleton? Skeleton = null;
    public SortedDictionary<string, ModelAnimation> MapAnimations;
    public SortedDictionary<string, ModelNode> MapNodes = new();

    public Matrix4x4[]? AllBakedMatrices = null; 
    
    public float Scale = 1.0f;
    
    /**
     * Convenience method to create a model from a single InstanceDesc
     */
    public Model(InstanceDesc instanceDesc)
    {
        ModelNode mnRoot = new()
        {
            Model = this,
            Parent = null,
            InstanceDesc = instanceDesc,
            Transform = new(true, 0xffff, Matrix4x4.Identity)
        };
        RootNode = mnRoot;
    }


    public ModelNode CreateNode()
    {
        return new()
        {
            Parent = null,
            Model = this,
            Index = _nextNodeIndex++
        };
    }


    public ModelAnimation CreateAnimation()
    {
        return new ModelAnimation()
        {
            Index = _nextAnimIndex++,
            FirstFrame = _nextAnimFrame,
        };
    }


    /**
     * For the classic skinned animation, find the first InstanceDesc node below the given root node.
     */
    private static ModelNode? _findInstanceDescNodeBelow(ModelNode mn)
    {
        if (mn.InstanceDesc != null)
        {
            return mn;
        }

        foreach (var mnChild in mn.Children)
        {
            var mnInstanceDescNode = _findInstanceDescNodeBelow(mnChild);
            if (mnInstanceDescNode != null)
            {
                return mnInstanceDescNode;
            }
        }

        return null;
    }


    /**
     * Find the closest instance desc node close to the animation node.
     */
    private static ModelNode? _findClosestInstanceDesc(ModelNode mn)
    {
        ModelNode? mnCurr = mn;

        while (mnCurr != null)
        {
            ModelNode? mnBelowCurr = _findInstanceDescNodeBelow(mnCurr);
            if (null != mnBelowCurr)
            {
                return mnBelowCurr;
            }

            mnCurr = mnCurr.Parent;
        }

        return null;
    }


    private static Matrix4x4 _computeGlobalTransform(ModelNode mn)
    {
        Matrix4x4 m4ParentTransform;
        if (mn.Parent != null)
        {
            m4ParentTransform = _computeGlobalTransform(mn.Parent);
        }
        else
        {
            m4ParentTransform = Matrix4x4.Identity;
        }

        m4ParentTransform = mn.Transform.Matrix * m4ParentTransform;

        return m4ParentTransform;
    }
    
    
    /**
     * Bake all animations for the given node.
     *
     * @param m4ParentTransform
     *     How do I transform from the model to my parent.
     * @param m4GlobalTransform
     *     How do I transform from root to the mesh.
     */
    private void _bakeRecursive(ModelNode me, in Matrix4x4 m4ParentTransform, in Matrix4x4 m4GlobalTransform, Matrix4x4 m4BoneToModel, ModelAnimation ma, uint frameno)
    {
        var skeleton = Skeleton!;
        
        /*
         * Find the appropriate bone.  
         */
        Bone? bone = null;
        uint boneIndex = 0; 
        // Matrix4x4 m4Model2Bone;
        // Matrix4x4 m4Bone2Model;
        if (skeleton.MapBones.TryGetValue(me.Name, out bone))
        {
            // m4Model2Bone = bone.Model2Bone;
            // m4Bone2Model = bone.Bone2Model;
            boneIndex = bone.Index;
        }
        else
        {
            // m4Model2Bone = Matrix4x4.Identity;
            // m4Bone2Model = Matrix4x4.Identity;
        }
        
        Matrix4x4 m4Anim;
        Matrix4x4.Invert(me.Transform.Matrix, out var m4InverseBone);
        // Note: Inverse
        Matrix4x4 m4MyBoneToModel = m4InverseBone * m4BoneToModel; 

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

            var m4Translate = Matrix4x4.CreateTranslation(kfPosition.Value);
            m4Anim =
                Matrix4x4.CreateFromQuaternion(kfRotation.Value)
                * Matrix4x4.CreateScale(kfScaling.Value)
                * Matrix4x4.CreateTranslation(kfPosition.Value);
        }
        else
        {
            m4Anim = me.Transform.Matrix;
        }
        
        /*
         * This is the complete transformation of this node,
         */
        // Note: Inverse
        Matrix4x4 m4MyTransform = m4Anim * m4ParentTransform;
        
        /*
         * Store resulting matrix if we have a bone that carries it.
         * Otherwise, just pass it on to the children.
         */
        if (bone != null)
        {
            /*
             * baked shall define how I come from mesh local position to bone
             * transformed mesh position
             *
             * Warning: For reasons I do not understand now, I wrote this in M*V order instead
             * of the usual V*M order in this project. Hence, the vertex shader also multiplies
             * M*V.
             *
             * Input to this matrix is the mesh before model to world transformation.
             * Ultimately, we want to apply the transformation of all bones in that space.
             *
             * 1- So first, m4MyTransform is the inverse mesh global transformation plus the transformations
             * to bone space until and including me.
             *
             * 2- m4Model2Bone is questionable and differs between platforms. Actually I would want to transform
             * from bone space back to mesh space.
             *
             * 3- Finally we apply the global transform from mesh space to model space and we're back.
             *
             * Unfortunately, as the "bone to model" matrix does not exist, we need to construct
             * it from the static matrices defining the pose transformation in the bones.
             */
            Matrix4x4 m4Baked =
                /*
                 * Go from global space to bone 
                 */
                m4MyBoneToModel
                /*
                 * 
                 */
                * m4MyTransform
                ;
            
            /*
             * For some strange reason, transferring matrices via ssbo does transpose the
             * matrix whereas passing matrix as uniform doesnt, or vice cersea.
             * So we must adjust for that.
             */

            {
                var arr = ma.BakedFrames[frameno].BoneTransformations;
                if (boneIndex < arr.Length)
                {
                    arr[boneIndex] = m4Baked;
                }
            }
            AllBakedMatrices[(ma.FirstFrame+frameno) * Skeleton.NBones + boneIndex] = m4Baked;
        }
        
        if (me.Children != null)
        {
            /*
             * Now call ourselves recursively for each of our children
             */
            foreach (var child in me.Children)
            {
                _bakeRecursive(child, m4MyTransform, m4GlobalTransform, 
                    m4MyBoneToModel, ma, frameno);
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
         * We assume there is only one instancedesc. Don't know if this is true
         * for all formats.
         */
        Matrix4x4 m4GlobalTransform;
        {
            ModelNode? mnInstanceDesc = _findInstanceDescNodeBelow(_mnRoot);
            if (null == mnInstanceDesc)
            {
                /*
                 * Well, if there is no instancedesc, there is no need to bake anything.
                 */
                Trace($"No instance desc for animation");
                return;
            }

            m4GlobalTransform = _computeGlobalTransform(mnInstanceDesc);
        }
        
        Matrix4x4 m4InverseGlobalTransform = MatrixInversion.Invert(m4GlobalTransform);
        
        AllBakedMatrices = new Matrix4x4[_nextAnimFrame * Skeleton.NBones];

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
            uint nFrames = UInt32.Max((uint)(duration * 60f), 1);
            ma.NFrames = nFrames;
            ma.BakedFrames = new ModelBakedFrame[ma.NFrames];
            // ma.AllBakedFrames = new Matrix4x4[ma.NFrames * Skeleton.NBones];
            for (int frameno = 0; frameno < nFrames; ++frameno)
            {
                ModelBakedFrame bakedFrame = new()
                {
                    BoneTransformations = new Matrix4x4[UInt32.Max(Skeleton.NBones, MAX_BONES)]
                };
                ma.BakedFrames[frameno] = bakedFrame;
            }

            /*
             * Now for this animation, for every frame, recurse through the bones.
             */
            for (uint frameno = 0; frameno < ma.NFrames; ++frameno)
            {
                /*
                 * I need to start with the inverse transform, as it will be reapplied in the end again
                 * by the renderer.
                 *
                 * Plus, I need to apply the scale (which I also could do later).
                 */
                _bakeRecursive(RootNode, 
                    m4InverseGlobalTransform * Scale, 
                    m4GlobalTransform, 
                    m4InverseGlobalTransform, 
                    ma, frameno);
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


    private void _polishChildrenRecursively(ModelNode mn)
    {
        if (mn.InstanceDesc != null)
        {
            mn.EntityData = 1;
        }
        if (mn.Children != null)
        {
            foreach (var mnChild in mn.Children)
            {
                _polishChildrenRecursively(mnChild);
                mn.EntityData |= mnChild.EntityData;
            }
        }
    }
    

    /**
     * Finish the model for use.
     */
    public void Polish()
    {
        _polishChildrenRecursively(RootNode);
    }
    

    public Model()
    {
    }
}
