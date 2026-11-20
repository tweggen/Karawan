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

        /*
         * KAR-484: the samples' real positions, from the segment itself. This used to divide
         * the extent by nHoriz - the point count, not the step count - and so took every
         * sample to be up to a grid step short of where it is, restarting at every fragment:
         * the carved groove wandered off the station axis by 8.6 m at the median and up to
         * 36 m over the shipped lines.
         */
        /*
         * Copy data from source to target, modifying it.
         *
         * Set the landscape height to the height of the intercity.
         */
        for (int tez = 0; tez < esTarget.nVert; tez++)
        {
            var z = esTarget.SampleZ(tez);

            for (int tex = 0; tex < esTarget.nHoriz; tex++)
            {
                var x = esTarget.SampleX(tex);

                ElevationPixel epxSource = erSource.Elevations[tez, tex];
                ElevationPixel epxDest = epxSource;

                bool hitsIntercity = engine.world.IntercityCorridor.IsInCarvedBand(
                    new Vector2(x, z), va, vb);

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
         * Does it roughly intersect? The stations' box grown by the carved band.
         */
        if (!_aabb.IntersectsXZ(aabb))
        {
            return false;
        }

        /*
         * Exactly: does the rectangle hold any of the band between the stations (KAR-484)?
         * The line's own +-Width/2 edges crossing its border was the question before, which
         * left the band uncarved in a neighbour the axis itself does not reach.
         */
        return engine.world.IntercityCorridor.BandTouches(
            new Vector2(aabb.AA.X, aabb.AA.Z), new Vector2(aabb.BB.X, aabb.BB.Z),
            _line.StationA.Pos2, _line.StationB.Pos2);
    }


    public IntercityTrackElevationOperator(
        in nogame.intercity.Line line,
        in string strKey
    )
    {
        _line = line;
        /*
         * The Y extent here is inert - ElevationOperatorIntersects only ever asks
         * IntersectsXZ - but it named ClusterA at BOTH stations, and it named a height
         * this operator does not write. It is the track and the vehicle over it now.
         */
        _aabb = new();
        _aabb.Add(line.StationA.Position with { Y = line.Height });
        _aabb.Add(line.StationA.Position with
            { Y = engine.world.IntercityLine.VehicleHeightOf(line.Height) });
        _aabb.Add(line.StationB.Position with { Y = line.Height });
        _aabb.Add(line.StationB.Position with
            { Y = engine.world.IntercityLine.VehicleHeightOf(line.Height) });
        /*
         * Grown by the carved band either side: the stations alone span no depth across an
         * axis-aligned line, which rejected every neighbour fragment the band reaches into.
         */
        float band = engine.world.IntercityCorridor.CarveHalfWidth;
        _aabb.Add(line.StationA.Position + new Vector3(-band, 0f, -band));
        _aabb.Add(line.StationA.Position + new Vector3(band, 0f, band));
        _aabb.Add(line.StationB.Position + new Vector3(-band, 0f, -band));
        _aabb.Add(line.StationB.Position + new Vector3(band, 0f, band));
        _strKey = strKey;
        // _rnd = new builtin.tools.RandomSource(strKey);
    }
}
