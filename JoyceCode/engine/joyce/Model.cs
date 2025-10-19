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
    public int MAX_BONES = 120;
    
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
    public ModelNode? BaseBone { get; private set; } = null;
    
    public Matrix4x4 FirstInstanceDescTransformWithInstance { get; private set; } = Matrix4x4.Identity;
    private Matrix4x4 _m4InverseFirstInstanceDescTransformWithInstance = Matrix4x4.Identity;

    public Matrix4x4 FirstInstanceDescTransformWoInstance { get; private set; } = Matrix4x4.Identity;
    private Matrix4x4 _m4InverseFirstInstanceDescTransformWoInstance = Matrix4x4.Identity;

    public Matrix4x4 BaseBoneTransformWithInstance { get; private set; } = Matrix4x4.Identity;
    private Matrix4x4 _m4InverseBaseBoneTransformWithInstance = Matrix4x4.Identity;
    private Matrix4x4 _m4BaseBoneBone2Model = Matrix4x4.Identity;
    
    public Matrix4x4 BaseBoneTransformWoInstance { get; private set; } = Matrix4x4.Identity;
    private Matrix4x4 _m4InverseBaseBoneTransformWoInstance = Matrix4x4.Identity;
    
    
    public ModelNodeTree ModelNodeTree { get; private set; } 

    public ModelAnimation CreateAnimation(ModelNode? mnRestPose)
    {
        return new ModelAnimation()
        {
            Index = _nextAnimIndex++,
            FirstFrame = _nextAnimFrame,
            RestPose = mnRestPose,
            CpuFrames = new()
        };
    }


    public enum BakeMode
    {
        Relative = 0,
        Absolute = 1,
        RelativeOnTop = 2
    }

    private bool _traceAnim = false;


    void _computeAnimFrame(in ModelAnimChannel mac, ref Matrix4x4 m4Anim, uint frameno)
    {
        var kfPosition = mac.LerpPosition(frameno);
        var kfRotation = mac.SlerpRotation(frameno);
        var kfScaling = mac.LerpScaling(frameno);
        var v4Scaling = kfScaling.Value;
        var v4Position = kfPosition.Value;
        var qRotation = kfRotation.Value;
        qRotation = new(qRotation.X, qRotation.Y, qRotation.Z, qRotation.W);
        if (_traceAnim &&  0 == frameno)
        {
            Trace($"First frame: {kfPosition.Value} {kfRotation.Value} {kfScaling.Value}");
        }

        var m4Scale = Matrix4x4.CreateScale(v4Scaling);
        var m4Rotation = Matrix4x4.CreateFromQuaternion(qRotation);
        var m4Translation = Matrix4x4.CreateTranslation(v4Position);
        m4Anim = m4Anim 
            * m4Scale 
            * m4Rotation
            * m4Translation
            ;
    }
    
    
    private int _bakeRecCount;
    
    
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
        ModelNode mnRestPose,
        ModelNodeTree mntModelPose,
        BakeMode bakeMode,
        Matrix4x4 m4BoneSpaceToRestPose,
        ModelAnimation ma, uint frameno)
    {
        var skeleton = Skeleton!;

        /*
         * Is the current node referenced as a bone that can influence vertices?
         * Some nodes might be animated without directly influencing any vertices,
         * others might not be animated at all.
         */
        Bone? bone = null;
        int boneIndex = -1;

        if (null == mnRestPose)
        {
            mnRestPose = mntModelPose.RootNode;
        }
        if (skeleton.MapBones.TryGetValue(mnRestPose.Name, out bone))
        {
            boneIndex = bone.Index;
        }

        Matrix4x4 m4MyModelPoseToBonePose;

        ModelNode? mnModelPose = null;
        if (mntModelPose.MapNodes.TryGetValue(mnRestPose.Name, out mnModelPose))
        {
        }
        else
        {
            if (!mnRestPose.Transform.Matrix.IsIdentity)
            {
                int a = 1;
            }
        }


        if (bone != null)
        {
            /*
             * TXWTODO: The inverse has a magnitude of 1, model2bone 0,1. Root node is 0.01 setup by me, first instancedesc node is 100 setup by assimp.
             * This does not match for this model, assimp root is considered twice.
             * 
             */
            m4MyModelPoseToBonePose = _m4InverseFirstInstanceDescTransformWoInstance * bone.Model2Bone;
        }
        else
        {
            /*
             * If bone is null, we would not translate anyway.
             */
            m4MyModelPoseToBonePose = Matrix4x4.Identity;
        }
        
        /*
         * Is there an animation applied to this node?
         * Then use it or concatenate it.
         */
        Matrix4x4 m4LocalAnim;
        Matrix4x4 m4MyBoneSpaceToRestPose;

        Matrix4x4 m4AnimBase;

        m4AnimBase = m4BoneSpaceToRestPose;

        if (mnModelPose != null && ma.MapChannels.TryGetValue(mnModelPose, out var mac))
        {
            /*
             * We do have an animation channel for this node.
             * So consider the animation below.
             *
             * Apply it to the matrix.<
             */
            m4LocalAnim = Matrix4x4.Identity;
            _computeAnimFrame(mac, ref m4LocalAnim, frameno);

            switch (bakeMode)
            {
                case BakeMode.Absolute:
                    m4MyBoneSpaceToRestPose = FirstInstanceDescTransformWoInstance * m4LocalAnim;
                    break;
                default:
                case BakeMode.Relative:
                    m4MyBoneSpaceToRestPose = m4LocalAnim * m4AnimBase;
                    break;
                case BakeMode.RelativeOnTop:
                    m4MyBoneSpaceToRestPose = m4LocalAnim * mnRestPose.Transform.Matrix * m4AnimBase;
                    break;
            }

            if (_traceAnim && frameno == 0)
            {
                Trace($"Anim.Matrix {m4LocalAnim}");
                Trace($"Rest Transform.Matrix {mnRestPose.Transform.Matrix}");
                Trace($"Inverse global w/o instance: {_m4InverseFirstInstanceDescTransformWoInstance}");
                Trace($"GlobalTransform: {m4MyBoneSpaceToRestPose}");
            }
        }
        else
        {
            m4MyBoneSpaceToRestPose = mnRestPose.Transform.Matrix * m4BoneSpaceToRestPose;
        }

        // TXWTODO: Write this.
        /*
         * Look, if we are supposed to store this node's anim frame model pose to bone pose to the
         * model animation.
         */
        
        /*
         * Store resulting matrix if we have a bone that carries it and thus might influence
         * vertices.
         *
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
                 * First transform the mesh to global as intended                    
                 */
                /*
                 * First from model coordinate space to bone local coordinate space.
                 * The vertex shader is called with the coordinates as stored in the mesh.
                 *
                 * This contains the scaling or matrix correction we apply because
                 * of the fbx local system, typically 0.01 
                 */
                FirstInstanceDescTransformWithInstance *
                
                /*
                 * TXWTODO: TODO
                 * FirstInstanceDesc contains the root scaling, modelposetobonepose
                 */
                
                /*
                 * Now transform this back to bone coordinate system.
                 * TXWTODO: Check, if this contains modified root scaling.
                 * answer: Yes, it does, from _m4Inverse...
                 */
                m4MyModelPoseToBonePose *
                
                /*
                 * Now transform bone space back to original coord system.
                 * TXWTODO: Check, if this contains modified root scaling.
                 * answer: I belive it also does 
                 */
                m4MyBoneSpaceToRestPose *
                _m4InverseFirstInstanceDescTransformWithInstance
                ;

            /*
             * For some strange reason, transferring matrices via ssbo does transpose the
             * matrix whereas passing matrix as uniform doesnt, or vice cersea.
             * So we must adjust for that.
             */

            {
                if (mnModelPose == null)
                {
                    int a = 1;
                    Trace($"Warning: Baking a pose without an inverse global transform.");
                }
                var arr = ma.BakedFrames[frameno].BoneTransformations;
                if (boneIndex < arr.Length)
                {
                    arr[boneIndex] = m4Baked;
                    if (_traceAnim && frameno == 0)
                    {
                        Trace($"Baked \"{mnRestPose.Name}\": {m4Baked}");
                    }
                }
                else
                {
                    // Does not trigger.
                    int a = 1;
                }
            }
            AllBakedMatrices[(ma.FirstFrame + frameno) * Skeleton.NBones + boneIndex] = m4Baked;

            if (mnRestPose.Children == null || mnRestPose.Children.Count == 0)
            {
                // TXWTODO: We have problems with the hands, let's look if the terminal leaf case is a problem.
                // Does not trigger at all
                int a = 1;
            }
        }

        /*
         * If we are supposed to store to bone transformations for this bone, store it.
         * This may be used to later track combat system collision points.
         */
        if (ma.CpuFrames.TryGetValue(mnRestPose.Name, out var cpuBakedFrames))
        {
            /*
             * This is for creating children which are attached to a particular bone.
             */
            
            if(Matrix4x4.Invert(m4MyBoneSpaceToRestPose, out var m4MyRestPoseToBoneSpace))
            {
                cpuBakedFrames[frameno] = _m4InverseBaseBoneTransformWithInstance * m4MyRestPoseToBoneSpace;
            }
            else
            {
                Warning($"Matrix for cpu for bone {mnRestPose.Name} could not be ingerted");
                cpuBakedFrames[frameno] = Matrix4x4.Identity;
            }
            
            /*
             * Model2Bone is { {M11:-0,8913993 M12:-0,013022288 M13:0,4530315 M14:0} {M21:-0,45304492 M22:-0,0020718572 M23:-0,8914853 M24:0} {M31:0,012547806 M32:-0,9999131 M33:-0,0040528774 M34:0} {M41:-15,525688 M42:-0,006530285 M43:164,30531 M44:1} }
             */
        }

        if (mnRestPose.Children != null)
        {
            /*
             * Now call ourselves recursively for each of our children
             */
            foreach (var child in mnRestPose.Children)
            {
                ++_bakeRecCount;
                if (_bakeRecCount >= 20)
                {
                    //return;
                }
                _bakeRecursiveNew(child,
                    mntModelPose,
                    bakeMode,
                    m4MyBoneSpaceToRestPose,
                    //mnRestPose.Transform.Matrix * m4BoneSpaceToRestPose,
                    ma, frameno);
            }
        }
    }

    
    /**
     * Compute frame accurate interpolations for all bones for all animations.
     */
    public void BakeAnimations(string? strModelBaseBone, List<string>? cpuNodes)
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
        
        AllBakedMatrices = new Matrix4x4[_nextAnimFrame * skeleton.NBones];
        for (int i = 0; i < AllBakedMatrices.Length; ++i) AllBakedMatrices[i] = Matrix4x4.Identity;

        
        /*
         * setup cpu nodes
         */
        HashSet<string> setCPUNodes = new();
        foreach (var cpuNode in cpuNodes ?? new List<string>())
        {
            setCPUNodes.Add(cpuNode);
        }
        
        /*
         * First, for all animations, create the arrays of matrices for
         * each bone per frame.,
         */
        foreach (var kvp in MapAnimations)
        {
            ModelAnimation ma = kvp.Value;
            Trace($"Loading animation {kvp.Key}");
            if (cpuNodes != null)
            {
                foreach (var cpuNode in cpuNodes)
                {
                    ma.CpuFrames.Add(cpuNode, new Matrix4x4[ma.NFrames]);
                }
            }

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
                    BoneTransformations = new Matrix4x4[Int32.Max(Skeleton.NBones, MAX_BONES)]
                };
                for (int i = 0; i < bakedFrame.BoneTransformations.Length; ++i)
                    bakedFrame.BoneTransformations[i] = Matrix4x4.Identity;
                ma.BakedFrames[frameno] = bakedFrame;
            }

            /*
             * Now for this animation, for every frame, recurse through the bones.
             */
            for (uint frameno = 0; frameno < ma.NFrames; ++frameno)
            {
                _bakeRecCount = 0;
                _bakeRecursiveNew(
                    ma.RestPose,
                    ModelNodeTree,
                    BakeMode.Relative,
                    Matrix4x4.Identity,
                    //m4InverseGlobalTransform,
                    //_m4Correction,
                    ma,
                    frameno);
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
        FirstInstanceDescTransformWithInstance = other.FirstInstanceDescTransformWithInstance;
        FirstInstanceDescTransformWoInstance = other.FirstInstanceDescTransformWoInstance;
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
    public void Polish(string ?strModelBaseBone)
    {
        _polishChildrenRecursively(ModelNodeTree.RootNode);
        
        /*
         * Setup Base bone
         */
        if (strModelBaseBone != null)
        {
            if (ModelNodeTree.MapNodes.TryGetValue(strModelBaseBone, out var mnBaseBone))
            {
                BaseBone = mnBaseBone;
                BaseBoneTransformWithInstance = BaseBone!.ComputeGlobalTransform();
                _m4BaseBoneBone2Model = Skeleton!.MapBones[strModelBaseBone].Bone2Model;
                Matrix4x4.Invert(BaseBoneTransformWithInstance,
                    out _m4InverseBaseBoneTransformWithInstance);
                if (BaseBone.Parent != null)
                {
                    BaseBoneTransformWoInstance = BaseBone.Parent.ComputeGlobalTransform();
                    Matrix4x4.Invert(BaseBoneTransformWoInstance, out _m4InverseBaseBoneTransformWoInstance);
                }
   
            }
            else
            {
                ErrorThrow<ArgumentException>($"Base bone {strModelBaseBone} not found.");
            }
        }


        if (FirstInstanceDescNode != null)
        {
            FirstInstanceDescTransformWithInstance = FirstInstanceDescNode.ComputeGlobalTransform();
            Matrix4x4.Invert(FirstInstanceDescTransformWithInstance,
                out _m4InverseFirstInstanceDescTransformWithInstance);
            if (FirstInstanceDescNode.Parent != null)
            {
                FirstInstanceDescTransformWoInstance = FirstInstanceDescNode.Parent.ComputeGlobalTransform();
                Matrix4x4.Invert(FirstInstanceDescTransformWoInstance, out _m4InverseFirstInstanceDescTransformWoInstance);
            }
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
