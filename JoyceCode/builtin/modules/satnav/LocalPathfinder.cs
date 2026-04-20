using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using builtin.modules.satnav.desc;
using engine.navigation;
using engine.tale;
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
    private static readonly engine.Dc _dc = engine.Dc.Pathfinding;

    public NavCursor Start;
    public NavCursor Target;
    private RoutingPreferences? _preferences;
    private TransportationType _transportType = TransportationType.Pedestrian;

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
        var baseCost = _realDistance(parent.Junction, njNext);
        var adjustedCost = _applyPreferenceMultiplier(nlToMe, baseCost);

        return new Node()
        {
            Junction = njNext,
            LaneToMe = nlToMe,
            Parent = parent,
            CostFromStart = parent.CostFromStart + adjustedCost,
            EstimateToEnd = _realDistance(njNext, Target.Junction)
        };
    }

    /// <summary>
    /// Apply routing preference multiplier to a lane's cost.
    /// </summary>
    private float _applyPreferenceMultiplier(NavLane lane, float baseCost)
    {
        if (_preferences == null)
            return baseCost;

        var multiplier = _preferences.ComputeCostMultiplier(lane, _transportType);
        return baseCost * multiplier;
    }


    private void _estimate(Node n)
    {
        n.EstimateToEnd = _realDistance(n.Junction, Target.Junction);
    }


    private Node _pathFind()
    {
        Trace(_dc, $"Starting A* from junction {Start.Junction.Position} to {Target.Junction.Position}");

        if (Start == Target || Start.Junction == Target.Junction || Start.Lane == Target.Lane)
        {
            Trace(_dc, $"Start and target are the same, returning immediately");
            return _listNodes.TakeFirst();
        }

        int nodesExplored = 0;
        while (true)
        {
            if (_listNodes.Count == 0)
            {
                // Detailed analysis of why pathfinding failed
                int visitedNodes = _dictNodes.Count(kvp => kvp.Value.IsVisited);
                int unvisitedNodes = _dictNodes.Count(kvp => !kvp.Value.IsVisited);

                var closestNode = _dictNodes.Values
                    .Where(n => n.IsVisited)
                    .OrderBy(n => n.EstimateToEnd)
                    .FirstOrDefault();

                string diagnosis = $"Disconnected networks: only reached {visitedNodes} junctions. " +
                    (closestNode != null
                        ? $"Closest reached: {closestNode.EstimateToEnd:F0}m from target"
                        : "No junctions reached");

                string failureMsg = $"No node found in A* list. Explored {nodesExplored} nodes. " +
                    $"Start junction has {Start.Junction.StartingLanes?.Count ?? 0} starting lanes. " +
                    $"Start pos={Start.Junction.Position}, Target pos={Target.Junction.Position}. " +
                    diagnosis;

                Trace(_dc, $"PATHFIND FAILURE: {failureMsg}");
                ErrorThrow<InvalidOperationException>(failureMsg);
            }

            Node n = _listNodes.TakeFirst();
            n.IsVisited = true;
            nodesExplored++;

            var startingLanes = n.Junction.StartingLanes;

            // Handle dead-end junctions by allowing reverse traversal on the incoming lane
            if (startingLanes == null || startingLanes.Count == 0)
            {
                if (n.LaneToMe != null && n.Parent != null)
                {
                    // We can reverse the lane we came from (dead-end street, can go back the way we came)
                    Trace(_dc, $"Dead-end junction at {n.Junction.Position}, reversing incoming lane");
                    var reverseTargetJunction = n.LaneToMe.Start;  // Go back to where we came from
                    var costFromParent = _realDistance(n.Junction, reverseTargetJunction);
                    var costNewFromStart = n.CostFromStart + costFromParent;

                    Node nReverse;
                    if (_dictNodes.TryGetValue(reverseTargetJunction, out nReverse))
                    {
                        if (!nReverse.IsVisited && costNewFromStart < nReverse.CostFromStart)
                        {
                            _listNodes.Remove(nReverse.TotalCost(), nReverse);
                            nReverse.Parent = n;
                            nReverse.LaneToMe = n.LaneToMe;  // Mark the reverse direction
                            nReverse.CostFromStart = costNewFromStart;
                            _listNodes.Add(nReverse.TotalCost(), nReverse);
                        }
                    }
                }
                continue;  // Move to next node in open list
            }

            foreach (var nlChild in startingLanes)
            {
                // Skip lanes not accessible to this transport type
                if (!nlChild.AllowedTypes.HasFlag(_transportType))
                    continue;

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
                        _listNodes.Remove(nChild.TotalCost(), nChild);
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
                    Trace(_dc, $"Found target junction after exploring {nodesExplored} nodes");
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
    
    
    public List<NavLane>? Pathfind()
    {
        if (Start == null || Target == null)
        {
            return null;
        }
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


    public LocalPathfinder(NavCursor ncStart, NavCursor ncTarget,
        RoutingPreferences? preferences = null,
        TransportationType transportType = TransportationType.Pedestrian)
    {
        Start = ncStart;
        Target = ncTarget;
        _preferences = preferences;
        _transportType = transportType;
    }
}