using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using builtin.modules.satnav.desc;
using static engine.Logger;

namespace builtin.modules.satnav;


internal class Node
{
    public Node? Parent = null;
    public NavLane? LaneToMe = null;
    public NavJunction Junction;
    public bool IsVisited = false;
    
    public float CostFromStart = 0f;
    public float EstimateToEnd = 0f;

    public float TotalCost() => CostFromStart + EstimateToEnd;
}

public class LocalPathfinder
{
    public NavCursor Start;
    public NavCursor Target;
    
    private Dictionary<NavJunction, Node> _dictNodes = new();
    private SortedMultiValue<float, Node> _listNodes = new();



    private float _distance(NavJunction a, NavJunction b)
    {
        return Single.Abs(a.Position.X - b.Position.X) 
            + Single.Abs(a.Position.Y - b.Position.Y);
    }


    private float _realDistance(NavJunction a, NavJunction b)
    {
        return (b.Position-a.Position).Length();
    }


    private Node _startNode(NavJunction njStart)
    {
        return new()
        {
            Junction = njStart,
            Parent = null
        };
    }


    private Node _childNode(Node parent, NavLane nlToMe, NavJunction njNext)
    {
        return new Node()
        {
            Junction = njNext,
            LaneToMe = nlToMe,
            Parent = parent,
            CostFromStart = parent.CostFromStart + _realDistance(parent.Junction, njNext),
            EstimateToEnd = _realDistance(njNext, Target.Junction)
        };
    }


    private void _estimate(Node n)
    {
        n.EstimateToEnd = _realDistance(n.Junction, Target.Junction);
    }


    private Node _pathFind()
    {
        if (Start == Target || Start.Junction == Target.Junction)
        {
            return _listNodes.TakeFirst();
        }
        
        while (true)
        {
            if (_listNodes.Count == 0)
            {
                ErrorThrow<InvalidOperationException>($"No node found in A* list.");
            }
            
            Node n = _listNodes.TakeFirst();
            n.IsVisited = true;

            foreach (var nlChild in n.Junction.StartingLanes)
            {
                var njChild = nlChild.End;
                Node nChild;
                
                var costFromParent = _realDistance(n.Junction, njChild);
                var costNewFromStart = n.CostFromStart + costFromParent;
                
                /*
                 * Do we already have the target junction listed?
                 */
                if (_dictNodes.TryGetValue(njChild, out nChild))
                {
                    /*
                    * If we already visited that node, we do not need
                    * to conitnue investigating it.
                    */
                    if (nChild.IsVisited)
                    {
                        continue;
                    }

                    /*
                    * We already visited that particular junction.
                    * Update it, if we can provide a shorter metric.
                    */
                    if (costNewFromStart < nChild.CostFromStart)
                    {
                        _listNodes.Remove(nChild.EstimateToEnd, nChild);
                        nChild.Parent = n;
                        nChild.LaneToMe = nlChild;
                        nChild.CostFromStart = costNewFromStart;
                        _listNodes.Add(nChild.TotalCost(), nChild);
                    }
                }
                else
                {
                    nChild = _childNode(n, nlChild, njChild);
                    _listNodes.Add(nChild.TotalCost(), nChild);
                    _dictNodes.Add(nChild.Junction, nChild);
                }

                if (njChild == Target.Junction)
                {
                    /*
                     * Maybe we reached the targvet junction but not the
                     * target lane yet.
                     */

                    if (nlChild != Target.Lane)
                    {
                        var nAdditionalChild = _childNode(n, Target.Lane, Target.Lane.End);
                        nAdditionalChild.Parent = nChild;
                        return nAdditionalChild;
                    }
                    else
                    {
                        return nChild;
                    }
                }
            }
            
        }
    }
    
    
    public List<NavLane> Pathfind()
    {
        var nStart = _startNode(Start.Junction);
        _dictNodes.Add(Start.Junction, nStart);
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
        
        List<NavLane> listLanes = new();
        while (lastNode != null)
        {
            NavLane? nl = lastNode.LaneToMe;
            if (nl != null)
            {
                listLanes.Add(nl);
            }
            lastNode = lastNode.Parent;
        }

        listLanes.Reverse();

        return listLanes;
    }


    public LocalPathfinder(NavCursor ncStart, NavCursor ncTarget)
    {
        Start = ncStart;
        Target = ncTarget;
    }
}