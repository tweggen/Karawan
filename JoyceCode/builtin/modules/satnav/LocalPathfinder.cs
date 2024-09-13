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

    private void _addOptions(NavJunction njStart, NavJunction njTarget)
    {
        foreach (var nl in njSource.StartingLanes)
        {
            var nj = nl.End;
            if (_hashVisitedJunction.Contains(nj))
            {
                continue;
            }
        }
    }


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


    private void _pathFind()
    {
        while (true)
        {
            if (_listNodes.Count == 0)
            {
                ErrorThrow<InvalidOperationException>($"No node found in A* list.");
                return;
            }
            
            Node n = _listNodes.First();

            foreach (var nlChild in n.Junction.StartingLanes)
            {
                var costFromParent = _distance(n.Junction, nlChild.End);
                var costNewFromStart = n.CostFromStart + costFromParent;

                /*
                 * Do we already have the target junction listed?
                 */
                if (_dictNodes.TryGetValue(nlChild.End, out var nExisting))
                {
                    /*
                     * We already visited that particular junction.
                     * Update it, if we can provide a shorter metric.
                     */
                    if (costNewFromStart < nExisting.CostFromStart)
                    {
                        _listNodes.Remove(nExisting.EstimateToEnd, nExisting);
                        nExisting.Parent = n;
                        nExisting.CostFromStart = costNewFromStart;
                        _listNodes.Add(nExisting.EstimateToEnd, nExisting);
                    }
                }
                else
                {
                    var nChild = _childNode(n, nlChild.End);
                    _listNodes.Add(nChild.EstimateToEnd, nChild);
                    _dictNodes.Add(nChild.Junction, nChild);
                }
            }
            
        }
    }
    
    
    public void Pathfind()
    {
        var nStart = _startNode(Start);
        _dictNodes.Add(Start, nStart);
        _listNodes.Add(nStart.TotalCost(), nStart);
        _estimate(nStart);

        _pathfind();
    }


    public LocalPathfinder(NavJunction njStart, NavJunction njTarget)
    {
        Start = njStart;
        Target = njTarget;
    }
}