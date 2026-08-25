using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using BepuUtilities;
using builtin.baking;
using builtin.extensions;
using builtin.loader;
using engine.joyce.components;
using MessagePack;
using static engine.Logger;

namespace engine.joyce;

/**
 * Represent a loaded or generated model.
 *
 * This contains
 * - general information about the model.
 * - a hierarchy of InstanceDescs.
 *
 * WP-4.1 - the baked mo-{hash} file stores the RAW graph only: identity, the node
 * tree and the skeleton. Everything below IsHierarchical is derived, and is
 * derived by Polish() from exactly that raw graph - the same call the FBX loader
 * makes as its last step. Persisting the derived matrices as well would create a
 * second source of truth that a future change to Polish could silently contradict.
 *
 * The animation collection is NOT stored here either. It belongs to the
 * ac-{hash} files, which are keyed by (model, animation pack) while this file is
 * keyed by the model alone - see builtin.baking.ModelFileName.
 */
[MessagePackObject(AllowPrivate = true)]
public partial class Model : IMessagePackSerializationCallbackReceiver
{
    [IgnoreMember]
    private static readonly engine.Dc _dc = engine.Dc.Animation;

    [Key(0)]
    public string Name = "";

    [Key(1)]
    public string ModelUrl = "";

    [IgnoreMember]
    public string? AnimationUrls = null;

    [Key(2)]
    public Skeleton? Skeleton = null;

    [Key(3)]
    public float Scale = 1.0f;

    [IgnoreMember]
    public bool IsHierarchical { get; private set; } = false;

    [IgnoreMember]
    public ModelNode? FirstInstanceDescNode { get; private set; } = null;

    [IgnoreMember]
    public Matrix4x4 FirstInstanceDescTransformWithInstance { get; private set; } = Matrix4x4.Identity;
    [IgnoreMember]
    public Matrix4x4 InverseFirstInstanceDescTransformWithInstance = Matrix4x4.Identity;

    [IgnoreMember]
    public Matrix4x4 FirstInstanceDescTransformWoInstance { get; private set; } = Matrix4x4.Identity;
    [IgnoreMember]
    public Matrix4x4 InverseFirstInstanceDescTransformWoInstance = Matrix4x4.Identity;

    [IgnoreMember]
    public Matrix4x4 BaseBoneTransformWithInstance { get; private set; } = Matrix4x4.Identity;
    [IgnoreMember]
    public Matrix4x4 InverseBaseBoneTransformWithInstance = Matrix4x4.Identity;
    //public Matrix4x4 BaseBoneBone2Model = Matrix4x4.Identity;

    //public Matrix4x4 BaseBoneTransformWoInstance { get; private set; } = Matrix4x4.Identity;
    //public Matrix4x4 InverseBaseBoneTransformWoInstance = Matrix4x4.Identity;

    [IgnoreMember]
    public ModelAnimationCollection AnimationCollection;

    [Key(4)]
    public ModelNodeTree ModelNodeTree { get; private set; }

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
        ModelUrl = other.ModelUrl;
        AnimationUrls = other.AnimationUrls;
        Skeleton = other.Skeleton;
        Scale = other.Scale;
        IsHierarchical = other.IsHierarchical;  
        FirstInstanceDescNode = other.FirstInstanceDescNode;
        FirstInstanceDescTransformWithInstance = other.FirstInstanceDescTransformWithInstance;
        InverseFirstInstanceDescTransformWithInstance = other.InverseFirstInstanceDescTransformWithInstance;
        FirstInstanceDescTransformWoInstance = other.FirstInstanceDescTransformWoInstance;
        InverseFirstInstanceDescTransformWoInstance = other.InverseFirstInstanceDescTransformWoInstance;
        BaseBoneTransformWithInstance = other.BaseBoneTransformWithInstance;
        InverseBaseBoneTransformWithInstance = other.InverseBaseBoneTransformWithInstance;
        AnimationCollection = other.AnimationCollection; 
        ModelNodeTree = other.ModelNodeTree;
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
    public void Polish(string? strModelBaseBone)
    {
        _polishChildrenRecursively(ModelNodeTree.RootNode);
        
        /*
         * Setup Base bone
         */
        if (AnimationCollection != null)
        {
            AnimationCollection.Polish(this, strModelBaseBone);
        }
        BaseBoneTransformWithInstance = BaseBoneTransformWithInstance;

        if (FirstInstanceDescNode != null)
        {
            FirstInstanceDescTransformWithInstance = FirstInstanceDescNode.ComputeGlobalTransform();
            Matrix4x4.Invert(FirstInstanceDescTransformWithInstance,
                out InverseFirstInstanceDescTransformWithInstance);
            if (FirstInstanceDescNode.Parent != null)
            {
                FirstInstanceDescTransformWoInstance = FirstInstanceDescNode.Parent.ComputeGlobalTransform();
                Matrix4x4.Invert(FirstInstanceDescTransformWoInstance, out InverseFirstInstanceDescTransformWoInstance);
            }
        }
    }


    public bool TryLoadModelAnimationCollection(out ModelAnimationCollection? animcoll)
    {
        animcoll = null;
        if (engine.GlobalSettings.Get("joyce.CompileMode") == "true")
        {
            return false;
        }
        
        /*
         * Allow disabling pre-baked animation loading via global settings.
         * Useful for debugging bone weight issues or forcing re-baking.
         */
        if (engine.GlobalSettings.Get("joyce.DisablePrebakedAnimations") == "true")
        {
            Trace(_dc,$"Pre-baked animations disabled via joyce.DisablePrebakedAnimations setting.");
            return false;
        }

        try
        {
            string strFileName =
                ModelAnimationCollectionReader.ModelAnimationCollectionFileName(
                    ModelUrl, AnimationUrls);
            using (var acStream = engine.Assets.Open(strFileName))
            {
                animcoll = ModelAnimationCollectionReader.Read(acStream);
                if (animcoll != null)
                {
                    AnimationCollection.TestBakedAnimationsFrom(animcoll); 
                }
            }
        }
        catch (Exception e)
        {
            Warning($"Exception while reading pre-baked data: {e}");
        }

        if (null == animcoll)
        {
            Trace(_dc,$"Cannot use baked for {ModelUrl} {AnimationUrls}");
            return false;
        }
        else
        {
            Trace(_dc,$"Loaded baked animations for {ModelUrl} {AnimationUrls}");
            return true;
        }
    }


    /**
     * Try to load this model from its baked mo-{hash} file instead of importing
     * the source asset (WP-4.3).
     *
     * Deliberately the same shape as TryLoadModelAnimationCollection above,
     * including the escape hatches: nothing is loaded in CompileMode, because that
     * is the run that PRODUCES these files, and joyce.DisablePrebakedModels forces
     * the import path for debugging - the mirror of joyce.DisablePrebakedAnimations.
     *
     * ModelUrl and AnimationUrls are re-stamped from the REQUEST rather than taken
     * from the file. AnimationUrls is not persisted at all (one baked model serves
     * every animation pack), and ModelUrl is what
     * ModelAnimationCollectionReader.ModelAnimationCollectionFileName hashes to
     * find the ac-{hash} companion - so a model loaded from a bake must still name
     * itself exactly as the caller asked for it, or it silently loses its
     * animations.
     */
    public static bool TryLoadBaked(
        string url, builtin.loader.ModelProperties? modelProperties, out Model? model)
    {
        model = null;

        if (engine.GlobalSettings.Get("joyce.CompileMode") == "true")
        {
            return false;
        }

        if (engine.GlobalSettings.Get("joyce.DisablePrebakedModels") == "true")
        {
            Trace(_dc, $"Pre-baked models disabled via joyce.DisablePrebakedModels setting.");
            return false;
        }

        string strFileName = ModelFileName.Of(System.IO.Path.GetFileName(url), modelProperties);

        try
        {
            using (var stream = engine.Assets.Open(strFileName))
            {
                /*
                 * Assets.Open returns null for a resource it does not know, rather
                 * than throwing. Handing that to MessagePack produces "Error
                 * occurred while reading from the stream", which describes the
                 * symptom and hides the cause - the file was never declared.
                 */
                if (null == stream)
                {
                    Warning($"No baked model {strFileName} is registered for {url}. "
                            + "Declare it with \"type\": \"model\" in the resource list, and make "
                            + "sure its modelProperties match the ones this call site loads with.");
                    return false;
                }

                model = ModelReader.Read(stream);
            }
        }
        catch (Exception e)
        {
            /*
             * A miss is recoverable only for as long as the source asset still
             * ships. Name the file that was wanted: the usual cause is a bake
             * identity that does not match the properties this call site loads
             * with, and that is not deducible from "model failed to load".
             */
            Warning($"Unable to read baked model {strFileName} for {url}: {e.Message}");
            model = null;
            return false;
        }

        if (null == model)
        {
            Warning($"Baked model {strFileName} for {url} was empty.");
            return false;
        }

        model.ModelUrl = url;
        model.AnimationUrls =
            modelProperties != null
            && modelProperties.Properties.TryGetValue("AnimationUrls", out var animationUrls)
                ? animationUrls
                : null;

        Trace(_dc, $"Loaded baked model {strFileName} for {url}.");
        return true;
    }


    /**
     * Finish a model that came out of a baked mo-{hash} file.
     *
     * This is the tail of FbxModel.Load, and it has to be: Polish derives the
     * instance-desc transforms the renderer uses, and BakeAnimations is what pulls
     * in the ac-{hash} companion. Skipping either gives a model that loads and
     * then renders in the wrong place with no animations.
     *
     * On Model rather than in ModelCache so the runtime and the tests exercise the
     * same code - a test that reimplemented these two calls would pass while the
     * real path did something else.
     */
    public void FinishBaked(builtin.loader.ModelProperties? modelProperties)
    {
        string? strModelBaseBone = null;
        List<string>? cpuNodes = null;

        if (modelProperties != null)
        {
            if (modelProperties.Properties.TryGetValue("ModelBaseBone", out var baseBone))
            {
                strModelBaseBone = baseBone;
            }

            if (modelProperties.Properties.TryGetValue("CPUNodes", out var strCpuNodes))
            {
                cpuNodes = strCpuNodes
                    .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .ToList();
            }
        }

        Polish(strModelBaseBone);

        try
        {
            BakeAnimations(strModelBaseBone, cpuNodes);
        }
        catch (Exception e)
        {
            Trace(_dc, $"Caught exception while baking animations for {ModelUrl}: {e}");
        }
    }


    /**
     * Bake the animation data as required by the vertex shader.
     * This one either loads it from the animation data cache / baked assets
     * or computes it..
     */
    public void BakeAnimations(string strModelBaseBone, List<string> cpuNodes)
    {
        bool haveBaked = false;

        try
        {

            if (TryLoadModelAnimationCollection(out var animcoll))
            {
                if (animcoll != null)
                {
                    AnimationCollection.UseBakedAnimationsFrom(animcoll);
                    haveBaked = true;
                }
            }
        }
        catch (Exception e)
        {
            Warning($"Exception while reading pre-baked data: {e}");
        }

        if (!haveBaked)
        {
            AnimationCollection.BakeAnimations(strModelBaseBone, cpuNodes);
            Trace(_dc,$"Manually baked animations for {ModelUrl} {AnimationUrls}");
        }
        else
        {
            Trace(_dc,$"Used baked animations for {ModelUrl} {AnimationUrls}");
        }

        /*
         * Whichever route we took, the frame offsets and the matrix array must describe
         * the same layout. If they do not, every animation lookup silently renders a
         * foreign pose, so say so loudly here rather than let it show up as a mystery
         * on screen.
         */
        AnimationCollection.ValidateBakedLayout(
            $"{ModelUrl} [{AnimationUrls}] ({(haveBaked ? "prebaked" : "manual")})",
            Skeleton?.NBones ?? 0);
    }

    public void DumpNodes()
    {
        ModelNodeTree.DumpNodes();
    }

    
    public void OnBeforeSerialize()
    {
    }


    /**
     * Reassemble what the file does not carry.
     *
     * Note the ORDER. Rebind must run before anything reads the tree, because
     * until it does every node's Parent is null and Model.Polish - which walks
     * upward through Parent to compute the instance desc transforms - would
     * compute identity for all of them and be wrong without failing.
     *
     * Polish itself is deliberately NOT called here: it needs the model base bone,
     * which is a load-time property rather than a property of the file. The baked
     * load path calls it, exactly where the FBX path does.
     */
    public void OnAfterDeserialize()
    {
        ModelNodeTree ??= new();
        ModelNodeTree.Rebind(this);

        /*
         * A deserialised model has no animation collection of its own - the
         * ac-{hash} file carries that. Give it an empty but non-null map, because
         * UseBakedAnimationsFrom merges INTO this map and would otherwise
         * dereference null the moment a baked animation file is adopted.
         */
        AnimationCollection ??= new(this);
        AnimationCollection.MapAnimations ??= new();
    }


    public Model()
    {
        ModelNodeTree = new();
        AnimationCollection = new(this);
    }
    
    
    /**
     * Convenience method to create a model from a single InstanceDesc
     */
    public Model(InstanceDesc instanceDesc)
    {
        ModelNodeTree = new(this, instanceDesc);
        AnimationCollection = new(this);
    }
}
