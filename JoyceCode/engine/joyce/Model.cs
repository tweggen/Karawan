using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using BepuUtilities;
using builtin.extensions;
using builtin.loader;
using engine.joyce.components;
using FbxSharp;
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


/**
 * Represent a loaded or generated model.
 *
 * This contains
 * - general information about the model.
 * - a hierarchy of InstanceDescs.
 */
public class Model
{
    public string Name = "";
    public uint MAX_BONES = 70;
    
    private int _nextAnimIndex = 1;
    private uint _nextAnimFrame = 0;

    public void PushAnimFrames(uint nFrames)
    {
        _nextAnimFrame += nFrames;
    }
    
    public Skeleton? Skeleton = null;
    public SortedDictionary<string, ModelAnimation> MapAnimations;
    
    public Matrix4x4[]? AllBakedMatrices = null; 
    
    public float Scale = 1.0f;
    
    public bool IsHierarchical { get; private set; } = false;

    public ModelNode? FirstInstanceDescNode { get; private set; } = null;
    
    public Matrix4x4 FirstInstanceDescTransform { get; private set; } = Matrix4x4.Identity;

    public ModelNodeTree ModelNodeTree { get; private set; } 
    
    
    public ModelAnimation CreateAnimation(ModelNode? mnRestPose)
    {
        return new ModelAnimation()
        {
            Index = _nextAnimIndex++,
            FirstFrame = _nextAnimFrame,
            RestPose = mnRestPose
        };
    }


    public enum BakeMode
    {
        Relative = 0,
        Absolute = 1
    }


    void _computeAnimFrame(in ModelAnimChannel mac, ref Matrix4x4 m4Anim, uint frameno)
    {
        var kfPosition = mac.LerpPosition(frameno);
        var kfRotation = mac.SlerpRotation(frameno);
        var kfScaling = mac.LerpScaling(frameno);
        m4Anim = m4Anim 
            * Matrix4x4.CreateScale(kfScaling.Value)
            * Matrix4x4.CreateFromQuaternion(kfRotation.Value)
            * Matrix4x4.CreateTranslation(kfPosition.Value)
            ;
    }
    
    
    /**
     * Bake all animations for the given node.
     *
     * @param m4GlobalTransform
     *     How do I transform from root to the mesh.
     * @param m4ModelSpaceToBoneSpace
     *     How do I transform from the model to the individual bone 
     * @param m4BoneSpaceToModelSpace
     *     How do I transform from the individual bone to the model.
     */
    private void _bakeRecursive(ModelNode me, 
        BakeMode bakeMode,
        Matrix4x4 m4GlobalTransform,
        Matrix4x4 m4ModelSpaceToPoseSpace, 
        Matrix4x4 m4BoneSpaceToModelSpace, 
        ModelAnimation ma, uint frameno)
    {
        var skeleton = Skeleton!;
        
        /*
         * Find the appropriate bone.  
         */
        Bone? bone = null;
        uint boneIndex = 0; 
        
        
        Matrix4x4 m4Model2Bone;
        Matrix4x4 m4Bone2Model;
        
        if (skeleton.MapBones.TryGetValue(me.Name, out bone))
        {
            m4Model2Bone = bone.Model2Bone;
            m4Bone2Model = bone.Bone2Model;
            boneIndex = bone.Index;
        }
        else
        {
            m4Model2Bone = Matrix4x4.Identity;
            m4Bone2Model = Matrix4x4.Identity;
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
            m4Anim = Matrix4x4.Identity;
            _computeAnimFrame(mac, ref m4Anim, frameno);
        }
        else
        {
            /*
             * In case my node cannot be found in the list of animation channels.
             */
            m4Anim = me.Transform.Matrix;
        }

        // Matrix4x4.Invert(m4Anim, out var m4InverseAnim);
        Matrix4x4.Invert(me.Transform.Matrix, out var m4InverseBone);
    
        Matrix4x4 m4MyBoneSpaceToModelSpace = m4Anim * m4BoneSpaceToModelSpace; 
        Matrix4x4 m4MyModelSpaceToPoseSpace = m4ModelSpaceToPoseSpace * m4InverseBone;

        /*
         * Store resulting matrix if we have a bone that carries it.
         * Otherwise, just pass it on to the children.
         */
        if (bone != null)
        {
            /*
             * baked shall define how I come from mesh local position to bone
             * transformed mesh position.
             *
             * In other words, after applying these transformations, also the instancedesc
             * transformations are applied, including all transformations "above" the instance
             * desc node.
             *
             * Warning: For reasons I do not understand now, I wrote this in M*V order instead
             * of the usual V*M order in this project. Hence, the vertex shader also multiplies
             * M*V.
             *
             * Input to this matrix is the mesh before model to world transformation.
             * Ultimately, we want to apply the transformation of all bones in that space.
             *
             * - apply my bone transformations until the moved me.
             * - apply the inverse to get back to the model
             *
             * Unfortunately, as the "bone to model" matrix does not exist, we need to construct
             * it from the static matrices defining the pose transformation in the bones.
             */
            Matrix4x4 m4Baked =
                /*
                 * First from model coordinate space to bone local coordinate space
                 */
#if true                   
                m4GlobalTransform *
                m4Model2Bone * 
#else                
                m4MyModelSpaceToPoseSpace *
#endif
                
                /*
                 * Go from global space to bone
                  */
                m4MyBoneSpaceToModelSpace
                ;
            // m4Baked = Matrix4x4.Transpose(m4Baked);
            // Matrix4x4.Invert(m4Baked, out var m4InverseBaked);
            
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
                else
                {
                    // Does not trigger.
                    int a = 1;
                }
            }
            AllBakedMatrices[(ma.FirstFrame+frameno) * Skeleton.NBones + boneIndex] = m4Baked;

            if (me.Children == null || me.Children.Count == 0)
            {
                // TXWTODO: We have problems with the hands, let's look if the terminal leaf case is a problem.
                // Does not trigger at all
                int a = 1;
            }
        }
        
        if (me.Children != null)
        {
            /*
             * Now call ourselves recursively for each of our children
             */
            foreach (var child in me.Children)
            {
                _bakeRecursive(child,
                    bakeMode,
                    m4GlobalTransform,
                    m4MyModelSpaceToPoseSpace,  
                    m4MyBoneSpaceToModelSpace, ma, frameno);
            }
        }
    }

    
    /**
     * Bake all animations for the given node.
     *
     * @param m4GlobalTransform
     *     How do I transform from root to the mesh.
     * @param m4ModelSpaceToBoneSpace
     *     How do I transform from the model to the individual bone 
     * @param m4BoneSpaceToModelSpace
     *     How do I transform from the individual bone to the model.
     */
    private void _bakeRecursiveNew(
        ModelNode me, 
        BakeMode bakeMode,
        Matrix4x4 m4BoneSpaceToRestPose,
        Matrix4x4 m4ModelPoseToBonePose, 
        ModelAnimation ma, uint frameno)
    {
        var skeleton = Skeleton!;
        
        /*
         * Find the appropriate bone.  
         */
        Bone? bone = null;
        uint boneIndex = 0; 

        if (skeleton.MapBones.TryGetValue(me.Name, out bone))
        {
            boneIndex = bone.Index;
        }
        else
        {
            int a = 1;
        }

        /*
         * Is there an animation stored inside this bone?
         * Then use it or concatenate it.
         */
        Matrix4x4 m4Anim;
        if (ma.MapChannels.TryGetValue(me, out var mac))
        {
            /*
             * We do have an animation channel for this node.
             * So consider the animation below.
             *
             * Apply it to the matrix.
             */
            m4Anim = Matrix4x4.Identity;
            _computeAnimFrame(mac, ref m4Anim, frameno);
        }
        else
        {
            /*
             * In case my node cannot be found in the list of animation channels.
             */
            m4Anim = me.Transform.Matrix;
        }

        Matrix4x4.Invert(me.Transform.Matrix, out var m4InverseBone);
    
        Matrix4x4 m4MyBoneSpaceToRestPose = m4Anim * m4BoneSpaceToRestPose; 
        Matrix4x4 m4MyModelPoseToBonePose = m4ModelPoseToBonePose * m4InverseBone;

        /*
         * Store resulting matrix if we have a bone that carries it.
         * Otherwise, just pass it on to the children.
         */
        if (bone != null)
        {
            /*
             * baked shall define how I come from mesh local position to bone
             * transformed mesh position.
             *
             * In other words, after applying these transformations, also the instancedesc
             * transformations are applied, including all transformations "above" the instance
             * desc node.
             *
             * Warning: For reasons I do not understand now, I wrote this in M*V order instead
             * of the usual V*M order in this project. Hence, the vertex shader also multiplies
             * M*V.
             *
             * Input to this matrix is the mesh before model to world transformation.
             * Ultimately, we want to apply the transformation of all bones in that space.
             *
             * - apply my bone transformations until the moved me.
             * - apply the inverse to get back to the model
             */
            Matrix4x4 m4Baked =
                /*
                 * First from model coordinate space to bone local coordinate space
                 */
                m4ModelPoseToBonePose * Matrix4x4.CreateScale(0.01f) *
                m4Anim *
                m4BoneSpaceToRestPose 
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
                else
                {
                    // Does not trigger.
                    int a = 1;
                }
            }
            AllBakedMatrices[(ma.FirstFrame+frameno) * Skeleton.NBones + boneIndex] = m4Baked;

            if (me.Children == null || me.Children.Count == 0)
            {
                // TXWTODO: We have problems with the hands, let's look if the terminal leaf case is a problem.
                // Does not trigger at all
                int a = 1;
            }
        }
        
        if (me.Children != null)
        {
            /*
             * Now call ourselves recursively for each of our children
             */
            foreach (var child in me.Children)
            {
                _bakeRecursiveNew(child,
                    bakeMode,
                    m4MyBoneSpaceToRestPose,
                    m4MyModelPoseToBonePose,
                    ma, frameno);
            }
        }
    }

    
    #if true
    public void _bakeNew(ModelAnimation ma)
    {
        Debug.Assert(ma.RestPose != null);
        Debug.Assert(ma.MapChannels != null);
        
        ModelNodeTree mntModelPose = ModelNodeTree;
        ModelNodeTree mntRestPose = ma.RestPose.ModelNodeTree;
        
        Skeleton skeleton = FindSkeleton();
        
        /*
         * Iterate through all animated nodes. 
         */
        foreach (var mac in ma.MapChannels.Values)
        {
            /*
             * Now, for each animated node find the node in the model pose
             * and the rest pose respectively.
             * Compute the model pose to bone and the bone to rest pose matrix.
             *
             * Note, that through all animated frames, the basic model and rest pose
             * matrices do not change, so we compute them just once.
             *
             * TXWTODO: Depending on the mode of operation, apply the matrices plus
             * plus the interpolated animation frame.
             */

            if (!mntModelPose.MapNodes.TryGetValue(mac.Target.Name, out var mnModelPose))
            {
                ErrorThrow<InvalidDataException>($"Node {mac.Target.Name} not found in model pose tree.");
            }
            if (!mntRestPose.MapNodes.TryGetValue(mac.Target.Name, out var mnRestPose))
            {
                ErrorThrow<InvalidDataException>($"Node {mac.Target.Name} not found in rest pose tree.");
            }
            
            Matrix4x4 m4InverseModelPose = Matrix4x4.Identity;
            mnModelPose.ComputeInverseGlobalTransform(ref m4InverseModelPose);
            
            Matrix4x4 m4RestPose = Matrix4x4.Identity;
            mnRestPose.ComputeGlobalTransform(ref m4RestPose);
            
            uint boneIndex = 0;
            Bone bone;

            if (skeleton.MapBones.TryGetValue(mnModelPose.Name, out bone))
            {
                boneIndex = bone.Index;
            }
            
            /*
             * How many real frames does this animation have?
             */
            float duration = ma.Duration;
            uint nFrames = UInt32.Max((uint)(duration * 60f), 1);
            ma.NFrames = nFrames;
            ma.BakedFrames = new ModelBakedFrame[ma.NFrames];
            
            for (uint frameno = 0; frameno < nFrames; ++frameno)
            {
                ModelBakedFrame bakedFrame = new()
                {
                    BoneTransformations = new Matrix4x4[UInt32.Max(Skeleton.NBones, MAX_BONES)]
                };
                ma.BakedFrames[frameno] = bakedFrame;
            }
            
            // TXWTODO: What, if there is no model anim channel?
            
            /*
             * Now for all frames apply the animation pose.
             */
            for (uint frameno = 0; frameno < nFrames; ++frameno)
            {
                Matrix4x4 m4FrameAnim = m4InverseModelPose;
                _computeAnimFrame(mac, ref m4FrameAnim, frameno);;
                m4FrameAnim = m4FrameAnim * m4RestPose;
                
                AllBakedMatrices![(ma.FirstFrame+frameno) * skeleton.NBones + boneIndex] = m4FrameAnim;
                
                // TXWTODO: This does not consider the position of upper hierarchy bnones.
            }
        }
    }
    #endif
    

    /**
     * Compute frame accurate interpolations for all bones for all animations.
     */
    public void BakeAnimations()
    {
        if (null == MapAnimations || null == Skeleton || MapAnimations.Count == 0)
        {
            return;
        }
        Trace($"Baking animations for {Name}");

        var skeleton = FindSkeleton();
        var mnRoot = ModelNodeTree.RootNode;
        
        /*
         * We assume there is only one instancedesc. Don't know if this is true
         * for all formats.
         */
        Matrix4x4 m4GlobalTransform;
        {
            ModelNode? mnInstanceDesc = mnRoot.FindInstanceDescNodeBelow();
            if (null == mnInstanceDesc)
            {
                /*
                 * Well, if there is no instancedesc, there is no need to bake anything.
                 */
                Trace($"No instance desc for animation");
                return;
            }

            m4GlobalTransform = mnInstanceDesc.ComputeGlobalTransform();
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
            Trace($"Loading animation {kvp.Key}");

            /*
             * How many real frames does this animation have?
             */
            float duration = ma.Duration;
            uint nFrames = UInt32.Max((uint)(duration * 60f), 1);
            ma.NFrames = nFrames;
            ma.BakedFrames = new ModelBakedFrame[ma.NFrames];
            
            for (int frameno = 0; frameno < nFrames; ++frameno)
            {
                ModelBakedFrame bakedFrame = new()
                {
                    BoneTransformations = new Matrix4x4[UInt32.Max(Skeleton.NBones, MAX_BONES)]
                };
                ma.BakedFrames[frameno] = bakedFrame;
            }
            
            /*
             * Use current implementation if no rest pose is given explicitely
             * If rest pose is not null, use different implementation that
             * considers rest pose.
             */
            if (ma.RestPose == null)
            {
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
                    _bakeRecursive(mnRoot,
                        BakeMode.Absolute,
                        /*
                         * m4GlobalTransform here is required to have the ochi person looking correctly
                         * with animations and not to be apart. It is however too large.
                         *
                         * Global transform already contains the scale factor.
                         */
                        m4GlobalTransform,
                        //Matrix4x4.Identity,

                        /*
                         * With these two commented out, scaling still is wrong.
                         */
                        //m4GlobalTransform,
                        //m4InverseGlobalTransform * Scale,
                        Matrix4x4.Identity,
                        // Matrix4x4.Identity,
                        m4InverseGlobalTransform,
                        //  m4GlobalTransform,  
                        ma, frameno);
                }
            }
            else
            {
#if false
                _bakeNew(ma);
#else
                /*
                 * Now for this animation, for every frame, recurse through the bones.
                 */
                for (uint frameno = 0; frameno < ma.NFrames; ++frameno)
                {
                    _bakeRecursiveNew(
                        mnRoot,
                        BakeMode.Absolute,
                        Matrix4x4.Identity, 
                        Matrix4x4.Identity, 
                        ma, 
                        frameno);
                }
#endif
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
        ModelNodeTree = other.ModelNodeTree;
        _nextAnimIndex = other._nextAnimIndex;
        _nextAnimFrame = other._nextAnimFrame;
        Skeleton = other.Skeleton;
        MapAnimations = other.MapAnimations;
        AllBakedMatrices = other.AllBakedMatrices;
        Scale = other.Scale;
        FirstInstanceDescNode = other.FirstInstanceDescNode;
        FirstInstanceDescTransform = other.FirstInstanceDescTransform;
        IsHierarchical = other.IsHierarchical;  
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
            if (FirstInstanceDescNode == null)
            {
                FirstInstanceDescNode = mn;
            }

            if (mn.Children != null && mn.Children.Count > 0)
            {
                IsHierarchical = true;
            }
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
        _polishChildrenRecursively(ModelNodeTree.RootNode);
        if (FirstInstanceDescNode != null)
        {
            FirstInstanceDescTransform = FirstInstanceDescNode.ComputeGlobalTransform();
        }
    }


    public void DumpNodes()
    {
        ModelNodeTree.DumpNodes();
    }

    
    public Model()
    {
        ModelNodeTree = new();
    }
    
    
    /**
     * Convenience method to create a model from a single InstanceDesc
     */
    public Model(InstanceDesc instanceDesc)
    {
        ModelNodeTree = new(this, instanceDesc);
    }
}
