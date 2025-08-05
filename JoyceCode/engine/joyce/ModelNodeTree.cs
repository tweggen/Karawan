using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using builtin.loader;
using static engine.Logger;

namespace engine.joyce;

/**
 * Represents a tree of model nodes.
 * Used to represent a pose.
 */
public class ModelNodeTree
{
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

    
    public SortedDictionary<string, ModelNode> MapNodes = new();
    
    public string DumpNodes()
    {
        string s = "";
        s += "{\n";
        s += "    \"nodes\": \n";
        if (RootNode != null)
        {
            s += RootNode.DumpNode();
        }
        else
        {
            s += "null,";
        }

        s += "},\n";

        return s;
    }
    
    private void _mergeInModelNodeTransformation(ModelNode mn, ModelNode mnNew, MergePolicy mp)
    {
        mn.Transform = mnNew.Transform;
    }


    private void _loadNodesRecursively(ModelNode mn, Skeleton? skeleton)
    {
        MapNodes[mn.Name] = mn;
        skeleton?.FindBone(mn.Name);
        
        if (mn.Children != null)
        {
            foreach (var mnChild in mn.Children)
            {
                _loadNodesRecursively(mnChild, skeleton);
            }
        }
    }
    

    public void SetRootNode(ModelNode mn, Skeleton? skeleton)
    {
        RootNode = mn;
        _loadNodesRecursively(mn, skeleton);
    }

    
    private void _addChildrenRecursively(ModelNode mn, MergePolicy mp, Skeleton? skeleton)
    {
        if (mn.Children != null)
        {
            foreach (var mnChild in mn.Children)
            {
                if (MapNodes.ContainsKey(mnChild.Name))
                {
                    ErrorThrow<InvalidDataException>($"Node {mnChild.Name} already exists in the model, structure clash.");
                }
                MapNodes[mnChild.Name] = mnChild;
                skeleton?.FindBone(mnChild.Name);
                _addChildrenRecursively(mnChild, mp, skeleton);
            }
        }
    }
        

    /**
     * Recursively merge in the given node into the model.
     * We, the parent node, are responsible for merging in the nodes into the MapNodes.
     */
    private void _mergeInModelNode(ModelNode mn, ModelNode mnNew, MergePolicy mp, Skeleton? skeleton)
    {
        /*
         * Step zero: Add this node to the node map if it did not exist.
         */
        bool didExist = MapNodes.TryGetValue(mnNew.Name, out var mnOld);
        
        /*
         * Step one: Make sure mn will have all the children mnNew already has.
         * Ensure that the new bones are part of the skeleton.
         */

        if (mnNew.Children != null) 
        {
            if (mn.Children == null)
            {
                mn.Children = new List<ModelNode>();
            }
            
            /*
             * We keep a list of children to add to this node.
             */
            List<ModelNode> newChildren = new(); 

            foreach (var mnNewChild in mnNew.Children)
            {
                /*
                 * If this model node exists in the current model, it has to have
                 * the same parent.
                 */
                bool nodeExistingModel = MapNodes.TryGetValue(mnNewChild.Name, out var mnOldByMap);

                ModelNode? mnOldChild = mn.Children.FirstOrDefault(mnCand => mnCand.Name == mnNewChild.Name);

                if (mnOldChild != null)
                {
                    /*
                     * If it was found in the parent node, it must also already exist in the node map.
                     */
                    if (!nodeExistingModel)
                    {
                        ErrorThrow<InvalidDataException>(
                            $"Node {mnNewChild.Name} was known to the parent but not in node map.");
                    }
                    
                    /*
                     * We need to merge an old child with a new one.
                     */
                    _mergeInModelNode(mnOldChild, mnNewChild, mp, skeleton);
                }
                else
                {
                    /*
                     * If it was not found in the parent node, it must not exist in the node map.
                     */
                    if (nodeExistingModel)
                    {
                        ErrorThrow<InvalidDataException>(
                            $"Node {mnNewChild.Name} was not known to the parent but in node map.");
                    }
                    MapNodes[mnNewChild.Name] = mnNewChild;

                    /*
                     * All new children need to have bones associated with them.
                     */
                    skeleton?.FindBone(mnNewChild.Name);
                    
                    /*
                     * This is a new child. Add it.
                     */
                    newChildren.Add(mnNewChild);
                    
                    _addChildrenRecursively(mnNewChild, mp, skeleton);
                }
                
            }

            mn.Children.AddRange(newChildren);
        }

        /*
         * Step two: Merge the actual contents.
         */
        {
            // TXWTODO: Care about the entity data

            /*
             * Children already are merged.
             */
            
            /*
             * Instance desc cannot be merged, only ovetwritten
             */
            if (mnNew.InstanceDesc != null)
            {
                if (mn.InstanceDesc == mnNew.InstanceDesc)
                {
                    ErrorThrow<InvalidDataException>($"Trying to merge two different instancedesc on {mn.Name}");
                }
                mn.InstanceDesc = mnNew.InstanceDesc;
            }
            
            /*
             * Finally, merge the transformation
             */
            _mergeInModelNodeTransformation(mn, mnNew, mp);
        }
    }


    /**
     * Merge in the given node into this model.
     */
    public void MergeInModelNode(ModelNode mnNew, MergePolicy mp, Skeleton? skeleton)
    {
        if (null == RootNode)
        {
            RootNode = mnNew;
            MapNodes[mnNew.Name] = mnNew;
        }
        _mergeInModelNode(RootNode, mnNew, mp, skeleton);
    }

    
    public ModelNode CreateNode(Model model)
    {
        return new()
        {
            Parent = null,
            Model = model,
            ModelNodeTree = this,
        };
    }
    
    public ModelNodeTree(Model model, InstanceDesc instanceDesc)
    {
        ModelNode mnRoot = new()
        {
            Model = model,
            ModelNodeTree = this,
            Parent = null,
            InstanceDesc = instanceDesc,
            Transform = new(true, 0xffff, Matrix4x4.Identity)
        };
    }
}