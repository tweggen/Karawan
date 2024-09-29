using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using engine.geom;

namespace builtin.modules.satnav.desc;


/**
 * This represents the actual navigable graph of a cluster.
 */
public class NavClusterContent
{
    private object _lo = new();

    public required NavCluster Cluster; 
    
    /**
     * The clusters contained inside this cluster.
     */
    public List<NavCluster> Clusters = new();
    
    /**
     * The actual junctions contained in this cluster.
     */
    public List<NavJunction> Junctions = new();
    
    /**
     * The lanes of this cluster.
     */
    public List<NavLane> Lanes = new();


    private AABB _aabb;
    private float _maxLaneLength;
    private Octree.PointOctree<NavJunction> _octreeJunctions;
    private Octree.BoundsOctree<NavLane> _octreeLanes;
    
    
    /*
     * Recreate the internal optzimization data stzructures.
     */
    public void Recompile()
    {
        /*
         * First, we need a boundary box of everything.
         */
        _aabb = new();
        _maxLaneLength = Single.MinValue;
        foreach (var nj in Junctions)
        {
            _aabb.Add(nj.Position);
        }

        foreach (var nc in Clusters)
        {
            _aabb.Add(nc.AABB);
        }

        var clusterSize = _aabb.Radius;
        if (0 == clusterSize)
        {
            int a = 1;
        }

        /*
         * Now generate the ocrrees with the adequate sizes.
         */
        _octreeJunctions = new(_aabb.Radius*2f, _aabb.Center, 2);
        foreach (var nj in Junctions)
        {
            _octreeJunctions.Add(nj, nj.Position);
        }

        _octreeLanes = new(_aabb.Radius*2f, _aabb.Center, 5f, 1f);
        foreach (var nl in Lanes)
        {
            Vector3 v3Size = nl.End.Position - nl.Start.Position;
            float laneLength = v3Size.Length();
            _maxLaneLength = Single.Max(laneLength, _maxLaneLength);
            v3Size = new Vector3(Single.Abs(v3Size.X), Single.Abs(v3Size.Y), Single.Abs(v3Size.Z));
            Vector3 v3Center = (nl.End.Position + nl.Start.Position) / 2f;
            Octree.BoundingBox bb = new Octree.BoundingBox(v3Center, v3Size);
            
            _octreeLanes.Add(nl, bb);
        }
        
    }
    

    public async Task<NavCursor> TryCreateCursor(Vector3 v3Position)
    {
        List<NavCluster> matchingClusters = new();
        
        /*
         * Look, if we should forward this call to a child. 
         */
        foreach (var nc in Clusters)
        {
            if (nc.AABB.Contains(v3Position))
            {
                matchingClusters.Add(nc);
            }
        }

        if (matchingClusters.Count > 0)
        {
            foreach (var ncl in matchingClusters)
            {
                var tCursor = await ncl.TryCreateCursor(v3Position);
                if (!tCursor.IsNil())
                {
                    return tCursor;
                }
            }
        }

        List<NavLane> tmpMatchList = new();
        
        /*
         * TXWTODO: Workaround to look close to the plane we cover here.
         */
        v3Position.Y = _aabb.Center.Y;

        /*
         * If we should no
         */
        if (!_octreeLanes.GetCollidingNonAlloc(
                tmpMatchList,
                new Octree.BoundingBox(v3Position, 2f * _maxLaneLength * Vector3.One)))
        {
            /*
             * Nothing found? Short circuit.
             */
            return NavCursor.Nil;
        }

        if (tmpMatchList.Count == 0)
        {
            return NavCursor.Nil;
        }

        float distClosest = Single.MaxValue;
        NavLane? nlClosest = null;
        foreach (var nl in tmpMatchList)
        {
            float dist = engine.geom.Line.Distance(nl.Start.Position, nl.End.Position, v3Position);
            if (dist < distClosest)
            {
                nlClosest = nl;
                distClosest = dist;
            }
        }

        if (null == nlClosest)
        {
            return NavCursor.Nil;
        }

        NavJunction njClosest;

        float dist2Start = (nlClosest.Start.Position - v3Position).LengthSquared();
        float dist2End = (nlClosest.End.Position - v3Position).LengthSquared();

        njClosest = (dist2Start <= dist2End) ? nlClosest.Start : nlClosest.End;
        
        return new NavCursor(Cluster)
        {
            Lane = nlClosest,
            Junction = njClosest
        };
    }
}
