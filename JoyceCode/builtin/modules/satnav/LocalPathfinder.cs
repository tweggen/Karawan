using System;
using System.Collections.Generic;
using System.Linq;
using builtin.modules.satnav.desc;
using static engine.Logger;

namespace builtin.modules.satnav;


internal class Node
{
    public NavJunction Junction;
    
    public Node? Parent = null;
    public float CostFromStart = 0f;
    public float EstimateToEnd = 0f;

    public float TotalCost() => CostFromStart + EstimateToEnd;
}

public class LocalPathfinder
{
    public NavJunction Start;
    public NavJunction Target;
    
    private Dictionary<NavJunction, Node> _dictNodes = new();
    private SortedMultiValue<float, Node> _listNodes = new();



    private float _distance(NavJunction a, NavJunction b)
    {
        return Single.Abs(a.Position.X - b.Position.X) 
            + Single.Abs(a.Position.Y - b.Position.Y);
    }


    private Node _startNode(NavJunction njStart)
    {
        return new()
        {
            Junction = njStart,
            Parent = null
        };
    }


    private Node _childNode(Node parent, NavJunction njNext)
    {
        return new Node()
        {
            Junction = njNext,
            Parent = parent,
            CostFromStart = parent.CostFromStart + _distance(parent.Junction, njNext),
            EstimateToEnd = _distance(njNext, Target)
        };
    }


    private void _estimate(Node n)
    {
        n.EstimateToEnd = _distance(n.Junction, Target);
    }


    private Node _pathFind()
    {
        while (true)
        {
            if (_listNodes.Count == 0)
            {
                ErrorThrow<InvalidOperationException>($"No node found in A* list.");
            }
            
            Node n = _listNodes.First();

            foreach (var nlChild in n.Junction.StartingLanes)
            {
                var njChild = nlChild.End;
                Node nChild;
                
                var costFromParent = _distance(n.Junction, njChild);
                var costNewFromStart = n.CostFromStart + costFromParent;
                
                /*
                 * Do we already have the target junction listed?
                 */
                if (_dictNodes.TryGetValue(njChild, out nChild))
                {
                    /*
                     * We already visited that particular junction.
                     * Update it, if we can provide a shorter metric.
                     */
                    if (costNewFromStart < nChild.CostFromStart)
                    {
                        _listNodes.Remove(nChild.EstimateToEnd, nChild);
                        nChild.Parent = n;
                        nChild.CostFromStart = costNewFromStart;
                        _listNodes.Add(nChild.EstimateToEnd, nChild);
                    }
                }
                else
                {
                    nChild = _childNode(n, njChild);
                    _listNodes.Add(nChild.EstimateToEnd, nChild);
                    _dictNodes.Add(nChild.Junction, nChild);
                }

                if (njChild == Target)
                {
                    return nChild;
                }
            }
            
        }
    }
    
    
    public List<NavJunction> Pathfind()
    {
        var nStart = _startNode(Start);
        _dictNodes.Add(Start, nStart);
        _listNodes.Add(nStart.TotalCost(), nStart);
        _estimate(nStart);

        /*
         * This results in a list of junctions.
         * The node returned represents the node that is closest to the
         * target, the last node in the list represents the junction closest
         * to the start.
         *
         * We return an unoptimized list containing the junctions between
         * but not including start and end.
         */
        var lastNode = _pathFind();
        
        List<NavJunction> listJunctions = new();
        while (lastNode != null)
        {
            listJunctions.Append(lastNode.Junction);
            lastNode = lastNode.Parent;
        }

        listJunctions.Reverse();

        return listJunctions;
    }


    public LocalPathfinder(NavJunction njStart, NavJunction njTarget)
    {
        Start = njStart;
        Target = njTarget;
    }
}