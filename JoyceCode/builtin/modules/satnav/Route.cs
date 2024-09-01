using System;
using System.Collections.Generic;
using System.Numerics;
using engine;
using engine.world;
using SharpNav;
using SharpNav.Pathfinding;

namespace builtin.modules.satnav;

public class Route : IDisposable
{
    public MapDB MapDB { get; }

    private IWaypoint _a;
    private IWaypoint _b;
    
    private Ref<NavMesh> _refNavMesh;

    private NavMeshQuery _navMeshQuery;

    private ClusterDesc _clusterDesc;
    
    public IWaypoint A
    {
        get
        {
            return _a;
        }
    }

    public IWaypoint B
    {
        get
        {
            return _b;
        }
    }


    public void Suspend()
    {
        
    }
    
    
    private bool InRange(Vector3 v1, Vector3 v2, float r, float h)
    {
	    float dx = v2.X - v1.X;
	    float dy = v2.Y - v1.Y;
	    float dz = v2.Z - v1.Z;
	    return (dx * dx + dz * dz) < (r * r) && Math.Abs(dy) < h;
    }
    
    
    private bool GetSteerTarget(NavMeshQuery navMeshQuery, Vector3 startPos, Vector3 endPos,
	    float minTargetDist, SharpNav.Pathfinding.Path path,
	    ref Vector3 steerPos, ref StraightPathFlags steerPosFlag, ref NavPolyId steerPosRef)
    {
	    StraightPath steerPath = new StraightPath();
	    navMeshQuery.FindStraightPath(startPos, endPos, path, steerPath, 0);
	    int nsteerPath = steerPath.Count;
	    if (nsteerPath == 0)
		    return false;

	    //find vertex far enough to steer to
	    int ns = 0;
	    while (ns < nsteerPath)
	    {
		    if ((steerPath[ns].Flags & StraightPathFlags.OffMeshConnection) != 0 ||
		        !InRange(steerPath[ns].Point.Position, startPos, minTargetDist, 1000.0f))
			    break;

		    ns++;
	    }

	    //failed to find good point to steer to
	    if (ns >= nsteerPath)
		    return false;

	    steerPos = steerPath[ns].Point.Position;
	    steerPos.Y = startPos.Y;
	    steerPosFlag = steerPath[ns].Flags;
	    if (steerPosFlag == StraightPathFlags.None && ns == (nsteerPath - 1))
		    steerPosFlag = StraightPathFlags.End; // otherwise seeks path infinitely!!!
	    steerPosRef = steerPath[ns].Point.Polygon;

	    return true;
    }


    /// <summary>
    /// Scaled vector addition
    /// </summary>
    /// <param name="dest">Result</param>
    /// <param name="v1">Vector 1</param>
    /// <param name="v2">Vector 2</param>
    /// <param name="s">Scalar</param>
    private void VMad(ref Vector3 dest, in Vector3 v1, in Vector3 v2, float s)
    {
	    dest.X = v1.X + v2.X * s;
	    dest.Y = v1.Y + v2.Y * s;
	    dest.Z = v1.Z + v2.Z * s;
    }
    

    public void Activate()
    {
        _refNavMesh = MapDB.FindNavMeshForCluster(_clusterDesc);
        _navMeshQuery = new NavMeshQuery(_refNavMesh.Value, 2048);
        
        
        Vector3 v3StartCenter = _a.GetLocation();
        Vector3 v3StartExtents = new(10f, 10f, 10f);
        _navMeshQuery.FindNearestPoly(ref v3StartCenter, ref v3StartExtents, out var npStart);
        
        Vector3 v3EndCenter = _b.GetLocation();
        Vector3 v3EndExtents = new(10f, 10f, 10f);
        _navMeshQuery.FindNearestPoly(ref v3EndCenter, ref v3EndExtents, out var npEnd);
        
        NavQueryFilter filter = new NavQueryFilter();
        int MAX_POLYS = 256;
        var path = new Path();
        _navMeshQuery.FindPath(ref npStart, ref npEnd, filter, path);
        
        //find a smooth path over the mesh surface
		int npolys = path.Count;
		Vector3 v3Iter = new();
		Vector3 v3Target = new();
		_navMeshQuery.ClosestPointOnPoly(npStart.Polygon, npStart.Position, ref v3Iter);
		_navMeshQuery.ClosestPointOnPoly(path[npolys - 1], npEnd.Position, ref v3Target);

		var smoothPath = new List<Vector3>(2048);
		smoothPath.Add(v3Iter);

		float STEP_SIZE = 0.5f;
		float SLOP = 0.01f;
		while (npolys > 0 && smoothPath.Count < smoothPath.Capacity)
		{
			//find location to steer towards
			Vector3 v3Steer = new ();
			StraightPathFlags steerPosFlag = 0;
			NavPolyId steerPosRef = NavPolyId.Null;

			if (!GetSteerTarget(_navMeshQuery, v3Iter, v3Target, SLOP, path, ref v3Steer, ref steerPosFlag, ref steerPosRef))
				break;

			bool endOfPath = (steerPosFlag & StraightPathFlags.End) != 0;
			bool offMeshConnection = (steerPosFlag & StraightPathFlags.OffMeshConnection) != 0;

			//find movement delta
			Vector3 delta = v3Steer - v3Iter;
			float len = (float)Math.Sqrt(Vector3.Dot(delta, delta));

			//if steer target is at end of path or off-mesh link
			//don't move past location
			if ((endOfPath || offMeshConnection) && len < STEP_SIZE)
				len = 1;
			else
				len = STEP_SIZE / len;

			Vector3 moveTgt = new Vector3();
			VMad(ref moveTgt, v3Iter, delta, len);

			//move
			Vector3 result = new Vector3();
			List<NavPolyId> visited = new List<NavPolyId>(16);
			NavPoint startPoint = new NavPoint(path[0], v3Iter);
			_navMeshQuery.MoveAlongSurface(ref startPoint, ref moveTgt, out result, visited);
			path.FixupCorridor(visited);
			npolys = path.Count;
			float h = 0;
			_navMeshQuery.GetPolyHeight(path[0], result, ref h);
			result.Y = h;
			v3Iter = result;

			//handle end of path when close enough
			if (endOfPath && InRange(v3Iter, v3Steer, SLOP, 1.0f))
			{
				//reached end of path
				v3Iter = v3Target;
				if (smoothPath.Count < smoothPath.Capacity)
				{
					smoothPath.Add(v3Iter);
				}
				break;
			}

			//store results
			if (smoothPath.Count < smoothPath.Capacity)
			{
				smoothPath.Add(v3Iter);
			}
		}
    }


    public void Dispose()
    {
    }


    public Route(MapDB mapDB, IWaypoint a, IWaypoint b)
    {
        MapDB = mapDB;
        _a = a;
        _b = b;
        _clusterDesc = ClusterList.Instance().GetClusterAt(_a.GetLocation());
    }
}
