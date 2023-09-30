using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using engine.elevation;
using SkiaSharp;
using static engine.Logger;


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
        in Rect erTarget
    )
    {
        var va = _line.StationA.Pos2;
        var vb = _line.StationB.Pos2;
        var vd = vb - va;
        var vp = new Vector2(vd.Y, -vd.X);
        var vup = Vector2.Normalize(vp);

        
        /*
         * Now that we have the average, read the level below us.
         */
        var erSource = elevationInterface.GetElevationRectBelow(
            erTarget.X0, erTarget.Z0,
            erTarget.X1, erTarget.Z1
        );

        /*
         * Now we iterate through our target, filling it with the
         * data from source, modifying the data along the way.
         */

        var stepX = (erTarget.X1 - erTarget.X0) / erTarget.nHoriz;
        var stepZ = (erTarget.Z1 - erTarget.Z0) / erTarget.nVert;

        //var minDist = Single.Max(_line.Width / 2f, stepX / 2f);
        var minDist = 2f*stepX;
        
        /*
         * Copy data from source to target, modifying it.
         *
         * Set the landscape height to the height of the intercity.
         */
        for (int tez = 0; tez < erTarget.nVert; tez++)
        {
            var z = erTarget.Z0
                    + ((erTarget.Z1 - erTarget.Z0) * tez)
                    / erTarget.nVert;

            for (int tex = 0; tex < erTarget.nHoriz; tex++)
            {
                /*
                 * Compute the absolute position derived from the target
                 * coordinates.
                 *
                 * Then check, wether this is within the bounds of the
                 * city.
                 */
                var x = erTarget.X0
                        + ((erTarget.X1 - erTarget.X0) * tex)
                        / erTarget.nHoriz;

                float resultHeight;
                float sourceHeight = erSource.Elevations[tez, tex];
                
                bool hitsIntercity = false;
                float dist = 0f;
                
                /*
                 * Compute distance from point to line.
                 */
                {
                    var vt = new Vector2(x, z);

                    vt -= va;

                    dist = Vector2.Dot(vt, vup);
                }

                if (Single.Abs(dist) <= minDist)
                {
                    hitsIntercity = true;
                    Trace($"Hits intercity at {x}, {z}");
                }

                if (hitsIntercity)
                {
                    resultHeight = _line.Height;
                }
                else
                {
                    /*
                     * This is not within the range of our city.
                     */
                    resultHeight = sourceHeight;
                }

                erTarget.Elevations[tez, tex] = resultHeight;
            }
        }
    }


    public bool ElevationOperatorIntersects(
        float x0, float z0,
        float x1, float z1)
    {
        /*
         * Does it roughly intersect?
         */
        engine.geom.AABB o = new();
        o.Add(new Vector3(x0, 0f, z0));
        o.Add(new Vector3(x1, 0f, z1));
        if (!_aabb.IntersectsXZ(o))
        {
            return false;
        }

        var vd = _line.StationB.Pos2 - _line.StationA.Pos2;
        var vp = new Vector2(vd.Y, -vd.X);
        var vup = Vector2.Normalize(vp);
        var lineWidthHalf = vup * (_line.Width / 2f);
        engine.geom.Line ll1 = new(_line.StationA.Pos2-lineWidthHalf, _line.StationB.Pos2-lineWidthHalf);
        engine.geom.Line ll2 = new(_line.StationA.Pos2+lineWidthHalf, _line.StationB.Pos2+lineWidthHalf);

        /*
         * Does it exactly intersect?
         */
        if (true
            && null == ll1.Intersect(new engine.geom.Line(new(x0, z0), new Vector2(x0, z1)))
            && null == ll1.Intersect(new engine.geom.Line(new(x0, z1), new Vector2(x1, z1)))
            && null == ll1.Intersect(new engine.geom.Line(new(x1, z1), new Vector2(x1, z0)))
            && null == ll1.Intersect(new engine.geom.Line(new(x1, z0), new Vector2(x0, z0)))
            && null == ll2.Intersect(new engine.geom.Line(new(x0, z0), new Vector2(x0, z1)))
            && null == ll2.Intersect(new engine.geom.Line(new(x0, z1), new Vector2(x1, z1)))
            && null == ll2.Intersect(new engine.geom.Line(new(x1, z1), new Vector2(x1, z0)))
            && null == ll2.Intersect(new engine.geom.Line(new(x1, z0), new Vector2(x0, z0)))
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
        // _rnd = new engine.RandomSource(strKey);
    }
}
