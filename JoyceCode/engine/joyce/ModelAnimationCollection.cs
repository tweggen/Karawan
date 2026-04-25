using MessagePack;
using System;
using System.Collections.Generic;
using System.Numerics;
using builtin.extensions;
using builtin.loader.fbx;
using static engine.Logger;

namespace engine.joyce;


/**
 * Contains a set of animations associated with a model.
 * The matrix data of a model animation collection may be
 * precomputed and restored from an asset.
 */
[MessagePackObject(AllowPrivate = true)]
public partial class ModelAnimationCollection
{
    private static readonly engine.Dc _dc = engine.Dc.Animation;

    [IgnoreMember]
    private Model _model;
    [Key(2)]
    private int _nextAnimIndex = 1;
    [Key(3)]
    private uint _nextAnimFrame = 0;
    
    [Key(0)]
    public SortedDictionary<string, ModelAnimation> MapAnimations;
    
    [Key(1)]
    public Matrix4x4[]? AllBakedMatrices = null; 
    

    public void PushAnimFrames(uint nFrames)
    {
        _nextAnimFrame += nFrames;
    }


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
    
    
        private static void _loadAnimBoneBase(
        string? strModelBaseBone, 
        ModelNodeTree mntRestPose, 
        out Matrix4x4 m4BaseToWorld,
        out Matrix4x4 m4WorldToBase)
    {
        if (strModelBaseBone != null)
        {
            if (mntRestPose.MapNodes.TryGetValue(strModelBaseBone, out var mnBaseBone))
            {
                //var mnBaseBone = mnBaseBone;
                m4BaseToWorld = mnBaseBone!.ComputeGlobalTransform();
                Matrix4x4.Invert(m4BaseToWorld,
                    out m4WorldToBase);
                if (mnBaseBone.Parent != null)
                {
                    //BaseBoneTransformWoInstance = mnBaseBone.Parent.ComputeGlobalTransform();
                    //Matrix4x4.Invert(BaseBoneTransformWoInstance, out _m4InverseBaseBoneTransformWoInstance);
                }

            }
            else
            {
                m4BaseToWorld = Matrix4x4.Identity;
                m4WorldToBase = Matrix4x4.Identity;
                ErrorThrow<ArgumentException>($"Base bone {strModelBaseBone} not found.");
            }
        }
        else
        {
            m4BaseToWorld = Matrix4x4.Identity;
            m4WorldToBase = Matrix4x4.Identity;
        }
    }

    [IgnoreMember]
    private bool _traceAnim = false;

    
    private static void _computeAnimFrame(in ModelAnimChannel mac, ref Matrix4x4 m4Anim, uint frameno)
    {
        #if false
        if (mac.ModelAnimation.Name.StartsWith("Run_InPlace") && mac.Target.Name.StartsWith("Elbow_"))
        {
            if (frameno == 1 || frameno == mac.ModelAnimation.NFrames - 1)
            {
                int a = 1;
            }
        }
        #endif
        var kfPosition = mac.LerpPosition(frameno);
        var kfRotation = mac.SlerpRotation(frameno);
        var kfScaling = mac.LerpScaling(frameno);
        var v4Scaling = kfScaling.Value;
        var v4Position = kfPosition.Value;
        var qRotation = kfRotation.Value;
        qRotation = new(qRotation.X, qRotation.Y, qRotation.Z, qRotation.W);
        var m4Scale = Matrix4x4.CreateScale(v4Scaling);
        var m4Rotation = Matrix4x4.CreateFromQuaternion(qRotation);
        var m4Translation = Matrix4x4.CreateTranslation(v4Position);
        m4Anim = m4Anim 
                 * m4Scale 
                 * m4Rotation
                 * m4Translation
            ;
    }
    
    
    public enum BakeMode
    {
        Relative = 0,
        Absolute = 1,
        RelativeOnTop = 2
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
        ModelNode mnRestPose,
        ModelNodeTree mntModelPose,
        BakeMode bakeMode,
        string? strModelBaseBone,
        Matrix4x4 m4BoneSpaceToRestPose,
        ModelAnimation ma, uint frameno)
    {
        var skeleton = _model.Skeleton!;

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
             * Assimp version compensation for bone offset matrix (mOffsetMatrix).
             *
             * Assimp computes: mOffsetMatrix = inverse(TransformLink) * absolute_transform
             *
             * Assimp 5.4.1: absolute_transform did NOT accumulate the intermediate pivot
             *   node transforms ($AssimpFbx$ chain nodes), so it only contained the parent's
             *   local transform. The formula InverseWoInstance * Model2Bone worked.
             *
             * Assimp 6.0.2 (commit f81ea69): absolute_transform now correctly accumulates
             *   all chain node transforms up to the mesh/model node level, so:
             *     Model2Bone_new = Model2Bone_old * mesh_local_chain
             *   where mesh_local_chain = inverse(WoInstance) * WithInstance.
             *   To recover the old working result, we strip the extra chain:
             *     InverseWoInstance * Model2Bone_new * inverse(mesh_local_chain)
             *   = InverseWoInstance * Model2Bone_new * InverseWithInstance * WoInstance
             *   = InverseWoInstance * Model2Bone_old  (same as the 5.4.1 formula)
             */
            if (AssimpVersionDetector.IsAssimp6OrNewer())
            {
                m4MyModelPoseToBonePose = _model.InverseFirstInstanceDescTransformWoInstance
                    * bone.Model2Bone
                    * _model.InverseFirstInstanceDescTransformWithInstance
                    * _model.FirstInstanceDescTransformWoInstance;
            }
            else
            {
                m4MyModelPoseToBonePose = _model.InverseFirstInstanceDescTransformWoInstance * bone.Model2Bone;
            }
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
                    m4MyBoneSpaceToRestPose = _model.FirstInstanceDescTransformWoInstance * m4LocalAnim;
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
                Trace(_dc,$"Anim.Matrix {m4LocalAnim}");
                Trace(_dc,$"Rest Transform.Matrix {mnRestPose.Transform.Matrix}");
                Trace(_dc,$"Inverse global w/o instance: {_model.InverseFirstInstanceDescTransformWoInstance}");
                Trace(_dc,$"GlobalTransform: {m4MyBoneSpaceToRestPose}");
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
                _model.FirstInstanceDescTransformWithInstance *
                
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
                _model.InverseFirstInstanceDescTransformWithInstance
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
                    Trace(_dc,$"Warning: Baking a pose without an inverse global transform.");
                }
                var arr = ma.BakedFrames[frameno].BoneTransformations;
                if (boneIndex < arr.Length)
                {
                    arr[boneIndex] = m4Baked;
                    if (_traceAnim && frameno == 0)
                    {
                        Trace(_dc,$"Baked \"{mnRestPose.Name}\": {m4Baked}");
                    }
                }
                else
                {
                    // Does not trigger.
                    int a = 1;
                }
            }
            AllBakedMatrices[(ma.FirstFrame + frameno) * _model.Skeleton!.NBones + boneIndex] = m4Baked;

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

            /*
             * We need to setup the transformation relative to the base bone,
             */
            _loadAnimBoneBase(strModelBaseBone, mnRestPose.ModelNodeTree,
                out var m4BaseToRestPose, out var m4RestPoseToBase);
            
            if(Matrix4x4.Invert(m4MyBoneSpaceToRestPose, out var m4MyRestPoseToBoneSpace))
            {
                /*
                 * TXWTODO: I only need the position and orientation of the hand, not the scale.
                 */
                Matrix4x4.Decompose(m4BoneSpaceToRestPose, out _, 
                    out var qBoneOrientation, out var v3BonePosition);
                
                Matrix4x4 m4BoneCpu =
                    Matrix4x4.CreateFromQuaternion(qBoneOrientation)
                    * Matrix4x4.CreateTranslation(v3BonePosition);
                cpuBakedFrames[frameno] = m4BoneCpu;
            }
            else
            {
                Warning($"Matrix for cpu for bone {mnRestPose.Name} could not be inserted");
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
                _bakeRecursiveNew(
                    child,
                    mntModelPose,
                    bakeMode,
                    strModelBaseBone,
                    m4MyBoneSpaceToRestPose,
                    //mnRestPose.Transform.Matrix * m4BoneSpaceToRestPose,
                    ma, frameno);
            }
        }
    }


    /**
     * Given a deserialized animation collection, use the pre-baked data
     * found.
     */
    public void UseBakedAnimationsFrom(ModelAnimationCollection o)
    {
        if (!TestBakedAnimationsFrom(o))
        {
            ErrorThrow<InvalidOperationException>($"Incompatible model animation collections found.");
        }
        AllBakedMatrices = o.AllBakedMatrices;
        foreach (var key in o.MapAnimations.Keys)
        {
            ModelAnimation ma;
            var oma = o.MapAnimations[key];
            if (!MapAnimations.ContainsKey(key))
            {
                MapAnimations.Add(key, oma);
                ma = oma;
            }
            else
            {
                ma = MapAnimations[key];
                ma.UseBakedAnimationsFrom(oma);
            }
        }
    }
    
    
    public bool TestBakedAnimationsFrom(ModelAnimationCollection o)
    {
        if (null == MapAnimations || 0 == MapAnimations.Count)
        {
            return true;
        }
        if (o._nextAnimIndex != _nextAnimIndex
            || o._nextAnimFrame != _nextAnimFrame
            || o.MapAnimations.Count != MapAnimations.Count)
        {
            Warning($"Incompatible model animation collections found.");
            return false;
        }
        else
        {
            return true;
        }
    }   
    

    
    /**
     * Compute frame accurate interpolations for all bones for all animations.
     */
    public void BakeAnimations(string? strModelBaseBone, List<string>? cpuNodes)
    {
        if (null == MapAnimations || null == _model.Skeleton || MapAnimations.Count == 0)
        {
            return;
        }

        var assimpVersion = AssimpVersionDetector.GetVersion();
        Trace(_dc, $"Baking animations for {_model.Name}, Skeleton has {_model.Skeleton.NBones} bones, Assimp version: {assimpVersion}");


        var skeleton = _model.FindSkeleton();
        var mnRoot = _model.ModelNodeTree.RootNode;
        
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
                Trace(_dc,$"No instance desc for animation");
                return;
            }

            m4GlobalTransform = mnInstanceDesc.ComputeGlobalTransform();
        }
        
        Matrix4x4 m4InverseGlobalTransform = MatrixInversion.Invert(m4GlobalTransform);

        /*
         * setup cpu nodes
         */
        HashSet<string> setCPUNodes = new();
        foreach (var cpuNode in cpuNodes ?? new List<string>())
        {
            setCPUNodes.Add(cpuNode);
        }

        /*
         * First pass: calculate all animation frame counts and update frame tracking.
         * This determines the total size needed for AllBakedMatrices.
         * Also update FirstFrame to reflect the correct offset in the global AllBakedMatrices array.
         */
        _nextAnimFrame = 0;  // Reset frame counter for clean baking
        uint currentFrameOffset = 0;
        foreach (var kvp in MapAnimations)
        {
            ModelAnimation ma = kvp.Value;
            float duration = ma.Duration;

            /*
             * ASSIMP VERSION INVESTIGATION (Phase 3):
             * Frame count calculation: nFrames = duration * 60 FPS
             *
             * Question: Does Duration differ between Assimp 5.4.1 and 6.0.2?
             * - ma.Duration = MDuration / MTicksPerSecond (set in FbxModel.cs:243)
             * - If the transformation chain changes (ce0a50e), could this affect animation duration?
             * - Unlikely, but profiling will confirm.
             *
             * For now, we use the same 60 FPS multiplier for both versions.
             * If profiles show different NFrames, we may need version-aware scaling.
             */
            uint nFrames = UInt32.Max((uint)(duration * 60f), 1);
            ma.NFrames = nFrames;
            ma.FirstFrame = currentFrameOffset;  // Set correct offset for this animation
            Trace(_dc, $"Animation '{kvp.Key}': Duration={duration}s, TicksPerSecond={ma.TicksPerSecond}, NFrames={nFrames}, FirstFrame={currentFrameOffset}");
            currentFrameOffset += nFrames;
            PushAnimFrames(nFrames);  // Update global frame counter for AllBakedMatrices indexing
        }
        Trace(_dc, $"Total animation frames: {_nextAnimFrame}, AllBakedMatrices will be size {_nextAnimFrame * skeleton.NBones}");

        // AllBakedMatrices allocation
        AllBakedMatrices = new Matrix4x4[_nextAnimFrame * skeleton.NBones];
        for (int i = 0; i < AllBakedMatrices.Length; ++i) AllBakedMatrices[i] = Matrix4x4.Identity;

        /*
         * Second pass: Create baked frame arrays and bake animation data.
         */
        foreach (var kvp in MapAnimations)
        {
            ModelAnimation ma = kvp.Value;
            Trace(_dc,$"Baking animation '{kvp.Key}': FirstFrame={ma.FirstFrame}, NFrames={ma.NFrames}, will write to indices {ma.FirstFrame * skeleton.NBones} to {(ma.FirstFrame + ma.NFrames - 1) * skeleton.NBones + skeleton.NBones - 1}");
            if (cpuNodes != null)
            {
                foreach (var cpuNode in cpuNodes)
                {
                    ma.CpuFrames.Add(cpuNode, new Matrix4x4[ma.NFrames]);
                }
            }

            ma.BakedFrames = new ModelBakedFrame[ma.NFrames];

            if (_model.Skeleton.NBones >= engine.joyce.Constants.MaxBones)
            {
                int a = 1;
            }
            
            for (int frameno = 0; frameno < ma.NFrames; ++frameno)
            {
                ModelBakedFrame bakedFrame = new()
                {
                    BoneTransformations = new Matrix4x4[
                        Int32.Max(_model.Skeleton.NBones, engine.joyce.Constants.MaxBones)]
                };
                for (int i = 0; i < bakedFrame.BoneTransformations.Length; ++i)
                    bakedFrame.BoneTransformations[i] = Matrix4x4.Identity;
                ma.BakedFrames[frameno] = bakedFrame;
            }

            /*
             * Now for this animation, for every frame, recurse through the bones.
             * 
             * IMPORTANT: We use the MESH's node tree (mnRoot) for recursion, not the animation's
             * RestPose. This ensures all bones in the mesh skeleton are visited, even if the
             * animation FBX doesn't include them (e.g., face bones in body-only animations).
             * Bones without animation channels will get their rest pose transform.
             */
            for (uint frameno = 0; frameno < ma.NFrames; ++frameno)
            {
                _bakeRecursiveNew(
                    mnRoot,  // Use mesh's node tree, not ma.RestPose!
                    _model.ModelNodeTree,
                    BakeMode.Relative,
                    strModelBaseBone,
                    Matrix4x4.Identity,
                    //m4InverseGlobalTransform,
                    //_m4Correction,
                    ma,
                    frameno);
            }
            
        }
    }



    public void Polish(Model model, string? strModelBaseBone)
    {
        _loadAnimBoneBase(strModelBaseBone, model.ModelNodeTree,
            out var m4BaseBoneTransformWithInstance,
            out model.InverseBaseBoneTransformWithInstance);

    }


    public ModelAnimationCollection(Model model)
    {
        _model = model;
    }


    public ModelAnimationCollection()
    {
        _model = null;
    }
}