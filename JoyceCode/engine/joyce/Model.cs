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
    
    public Skeleton? Skeleton = null;
    
    public float Scale = 1.0f;
    
    public bool IsHierarchical { get; private set; } = false;

    public ModelNode? FirstInstanceDescNode { get; private set; } = null;
    public ModelNode? BaseBone { get; private set; } = null;
    
    public Matrix4x4 FirstInstanceDescTransformWithInstance { get; private set; } = Matrix4x4.Identity;
    public Matrix4x4 InverseFirstInstanceDescTransformWithInstance = Matrix4x4.Identity;

    public Matrix4x4 FirstInstanceDescTransformWoInstance { get; private set; } = Matrix4x4.Identity;
    public Matrix4x4 InverseFirstInstanceDescTransformWoInstance = Matrix4x4.Identity;

    public Matrix4x4 BaseBoneTransformWithInstance { get; private set; } = Matrix4x4.Identity;
    public Matrix4x4 InverseBaseBoneTransformWithInstance = Matrix4x4.Identity;
    public Matrix4x4 BaseBoneBone2Model = Matrix4x4.Identity;
    
    public Matrix4x4 BaseBoneTransformWoInstance { get; private set; } = Matrix4x4.Identity;
    public Matrix4x4 InverseBaseBoneTransformWoInstance = Matrix4x4.Identity;
    
    public ModelAnimationCollection AnimationCollection; 
    
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
        ModelNodeTree = other.ModelNodeTree;
        Skeleton = other.Skeleton;
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
        AnimationCollection = new(this);
    }
}
