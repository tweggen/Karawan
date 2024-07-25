using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using engine.elevation;
using SkiaSharp;
using static engine.Logger;
using static builtin.Workarounds;

namespace nogame.intercity;

/**
 * Build intercity tracks outside of the city.
 */
public class IntercityTrackElevationOperator : IOperator
{
    /**
     * This is the city cluster I am associated with.
     */
    private nogame.intercity.Line _line;

    /**
     * The engine line's aabb for very fast tests.
     */
    private engine.geom.AABB _aabb; 

    private string _strKey;

    /**
     * Test operator to test operator chaining. This operator evens
     * out everything below the average height of a city to the average.
     *
     * Doesn't really make sense, just a test.
     */
    public void ElevationOperatorProcess(
        in IElevationProvider elevationInterface,
        in ElevationSegment esTarget
    )
    {
        var va = _line.StationA.Pos2;
        var vb = _line.StationB.Pos2;
        var vd = vb - va;
        var vp = new Vector2(vd.Y, -vd.X);
        var vup = V2Normalize(vp);

        
        /*
         * Now that we have the average, read the level below us.
         */
        var erSource = elevationInterface.GetElevationSegmentBelow(
            esTarget.Rect2
        );

        /*
         * Now we iterate through our target, filling it with the
         * data from source, modifying the data along the way.
         */

        var stepX = (esTarget.Rect2.B.X - esTarget.Rect2.A.X) / esTarget.nHoriz;
        var stepZ = (esTarget.Rect2.B.Y - esTarget.Rect2.A.Y) / esTarget.nVert;

        //var minDist = Single.Max(_line.Width / 2f, stepX / 2f);
        var minDist = 2f*stepX;
        
        /*
         * Copy data from source to target, modifying it.
         *
         * Set the landscape height to the height of the intercity.
         */
        for (int tez = 0; tez < esTarget.nVert; tez++)
        {
            var z = esTarget.Rect2.A.Y
                    + ((esTarget.Rect2.B.Y - esTarget.Rect2.A.Y) * tez)
                    / esTarget.nVert;

            for (int tex = 0; tex < esTarget.nHoriz; tex++)
            {
                /*
                 * Compute the absolute position derived from the target
                 * coordinates.
                 *
                 * Then check, wether this is within the bounds of the
                 * city.
                 */
                var x = esTarget.Rect2.A.X
                        + ((esTarget.Rect2.B.X - esTarget.Rect2.A.X) * tex)
                        / esTarget.nHoriz;

                ElevationPixel epxSource = erSource.Elevations[tez, tex];
                ElevationPixel epxDest = epxSource;
                
                bool hitsIntercity = false;
                float dist = 0f;
                
                /*
                 * Compute distance from point to line.
                 */
                {
                    var vt = new Vector2(x, z);

                    vt -= va;

                    dist = V2Dot(vt, vup);
                }

                if (Single.Abs(dist) <= minDist)
                {
                    hitsIntercity = true;
                    // Trace($"Hits intercity at {x}, {z}");
                }

                if (hitsIntercity)
                {
                    epxDest.Height = _line.Height;
                    epxDest.Biome = 2;
                }

                esTarget.Elevations[tez, tex] = epxDest;
            }
        }
    }


    public bool ElevationOperatorIntersects(engine.geom.AABB aabb)
    {
        /*
         * Does it roughly intersect?
         */
        if (!_aabb.IntersectsXZ(aabb))
        {
            return false;
        }

        var vd = _line.StationB.Pos2 - _line.StationA.Pos2;
        var vp = new Vector2(vd.Y, -vd.X);
        var vup = V2Normalize(vp);
        var lineWidthHalf = vup * (_line.Width / 2f);
        engine.geom.Line ll1 = new(_line.StationA.Pos2-lineWidthHalf, _line.StationB.Pos2-lineWidthHalf);
        engine.geom.Line ll2 = new(_line.StationA.Pos2+lineWidthHalf, _line.StationB.Pos2+lineWidthHalf);

        Vector2 v1 = new(aabb.AA.X, aabb.AA.Z);
        Vector2 v2 = new(aabb.AA.X, aabb.BB.Z);
        Vector2 v3 = new(aabb.BB.X, aabb.BB.Z);
        Vector2 v4 = new(aabb.BB.X, aabb.AA.Z);
        engine.geom.Line l1 = new(v1, v2); 
        engine.geom.Line l2 = new(v2, v3); 
        engine.geom.Line l3 = new(v3, v4); 
        engine.geom.Line l4 = new(v4, v1); 
        /*
         * Does it exactly intersect?
         */
        if (true
            && null == ll1.Intersect(l1) && null == ll1.Intersect(l2) && null == ll1.Intersect(l3) && null == ll1.Intersect(l4)
            && null == ll2.Intersect(l1) && null == ll2.Intersect(l2) && null == ll2.Intersect(l3) && null == ll2.Intersect(l4)
        )
        {
            return false;
        }

        return true;
    }


    public IntercityTrackElevationOperator(
        in nogame.intercity.Line line,
        in string strKey
    )
    {
        _line = line;
        _aabb = new();
        _aabb.Add(line.StationA.Position with { Y=line.ClusterA.AverageHeight });
        _aabb.Add(line.StationA.Position with { Y=line.ClusterA.AverageHeight+20f });
        _aabb.Add(line.StationB.Position with { Y=line.ClusterA.AverageHeight });
        _aabb.Add(line.StationB.Position with { Y=line.ClusterA.AverageHeight+20f });
        _strKey = strKey;
        // _rnd = new builtin.tools.RandomSource(strKey);
    }
}
