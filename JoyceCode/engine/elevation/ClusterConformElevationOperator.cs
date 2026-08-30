using System;
using System.Collections.Generic;
using System.Numerics;
using engine.streets;
using static engine.Logger;

namespace engine.elevation;


/**
 * Makes the ground under a city agree with the roads on it.
 *
 * A terrain-following city (joyce.DisableClusterFlattening) relaxes its street heights
 * to gradients a road could actually be built on, and then leaves the terrain exactly as
 * the noise produced it. The two therefore disagree by whatever GradeRelaxer took out:
 * a road slices through the hillside it crosses and stands proud of the dip it spans.
 * This pass grades the city site toward the street height field so that a road sits on
 * ground rather than in or over it.
 *
 * **This is §2c of STREETS-3D-TOPOLOGY, and it is not the corridor that section
 * originally described.** See StreetHeightField for why a corridor is not expressible on
 * a 20 m elevation grid.
 *
 * ## Why this breaks the ordering cycle
 *
 * Streets read the terrain and the terrain now reads the streets, which is a cycle right
 * up until the existing layer mechanism is used for what it is for. Elevation operators
 * register at ordered layer strings and each reads the layers strictly BELOW its own:
 *
 *     /000002/fillGrid            base terrain
 *     /000100/flattenCluster/...  ClusterBaseElevationOperator - average, biome, flatten
 *     /000150/conformCluster/...  THIS
 *     /000200/intercityTrails/... the intercity network
 *
 * TerrainStreetHeight samples Layer rather than TOP_LAYER, so junction heights are read
 * from the terrain as it was BEFORE any city conformed it. Street generation therefore
 * cannot reach this operator, and this operator is free to ask for the street graph.
 * Everything else in the game - rendering, GetWalkingHeightAt, the hover probe's terrain
 * fallback - still reads TOP_LAYER and sees the conformed result.
 *
 * **Below intercity, not above.** IntercityTrackElevationOperator hard-sets the terrain
 * to its line's own constant height along a narrow band, which is an absolute override
 * and not a shape a city may smooth away. Keeping it last also means its relationship to
 * the city terrain is exactly what it has always been - it overwrote the flat plateau
 * before, and it overwrites the graded site now.
 */
public class ClusterConformElevationOperator : IOperator
{
    private static readonly engine.Dc _dc = engine.Dc.StreetGen;


    /**
     * The layer these operators live on, and - because a layer string is read as
     * "everything strictly below this" - also the layer TerrainStreetHeight samples.
     *
     * One constant for both halves on purpose. The registrations sort AFTER it (they
     * extend it), the flattening layer sorts BEFORE it, and both facts are what make
     * "streets read unconformed ground" true. Splitting them into two spellings is how
     * that stops being true without anything failing to compile.
     */
    public const string Layer = Cache.LAYER_BASE + "/000150";


    private readonly world.ClusterDesc _clusterDesc;
    private readonly string _strKey;

    private readonly object _lo = new();
    private StreetHeightField _field;


    /**
     * The city rectangle grown by the grading radius, so that the skirt where the graded
     * site meets untouched terrain is computed rather than clipped off at the city
     * boundary.
     */
    private geom.AABB _aabb;


    public void ElevationOperatorProcess(
        in IElevationProvider elevationInterface,
        in ElevationSegment esTarget
    )
    {
        var erSource = elevationInterface.GetElevationSegmentBelow(esTarget.Rect2);

        /*
         * Nowhere near this city, so nothing to grade.
         *
         * The "first layer below" search does test this, but the TOP layer's operator is
         * run for every fragment in the world without one - see the disabled intersection
         * check in Cache.ElevationCacheGetAt - and this is the top layer in any world with
         * no intercity network registered above it. Without this the first fragment loaded
         * anywhere would generate a city's whole street graph to grade ground a kilometre
         * away from it.
         */
        if (!ElevationOperatorIntersects(new geom.AABB(esTarget.Rect2)))
        {
            _copy(erSource, esTarget);
            return;
        }

        /*
         * A flat city has already been ironed flat by the operator below this one, and
         * every junction of it is at the average by construction - so there is nothing
         * for the streets to pull the ground toward. Tested rather than asserted, and
         * checked BEFORE StrokeStore() so that the flat path never triggers street
         * generation from inside an elevation operator either.
         *
         * Belt and braces: with the flag off this operator is not registered at all.
         */
        if (_clusterDesc.StreetHeightSource.IsFlat)
        {
            _copy(erSource, esTarget);
            return;
        }

        StreetHeightField field = _ensureField();
        if (0 == field.SpanCount)
        {
            _copy(erSource, esTarget);
            return;
        }

        Grade(field, new Vector2(_clusterDesc.Pos.X, _clusterDesc.Pos.Z), erSource, esTarget);
    }


    /**
     * The write itself, with the cluster, the engine and the elevation cache taken out of
     * it, so that what it does to a pixel can be tested against plain data.
     *
     * @param v2Origin
     *     World position of the cluster's centre, since the field is in cluster relative
     *     coordinates and an elevation segment is not.
     */
    internal static void Grade(
        StreetHeightField field,
        in Vector2 v2Origin,
        ElevationSegment erSource,
        ElevationSegment esTarget)
    {
        /*
         * Sample spacing over the segment's own extent.
         *
         * Deliberately nHoriz - 1 and not nHoriz. A segment carries GroundResolution + 1
         * samples spanning FragmentSize, so the LAST sample sits on the far edge and the
         * step is FragmentSize / GroundResolution - which is exactly the spacing
         * CacheEntry.GetElevationPixelAt reads them back at. The two sibling operators
         * divide by nHoriz instead and so place their samples about five percent short,
         * restarting the error at every fragment. They get away with it because each
         * writes a CONSTANT inside a rectangle, so the only consequence is a boundary
         * ragged by a sample width. Here it would be a step in the graded ground along
         * every fragment seam, which is the artefact this whole pass exists to remove.
         */
        float stepX = esTarget.nHoriz > 1
            ? (esTarget.Rect2.B.X - esTarget.Rect2.A.X) / (esTarget.nHoriz - 1)
            : 0f;
        float stepZ = esTarget.nVert > 1
            ? (esTarget.Rect2.B.Y - esTarget.Rect2.A.Y) / (esTarget.nVert - 1)
            : 0f;

        for (int tez = 0; tez < esTarget.nVert; tez++)
        {
            float z = esTarget.Rect2.A.Y + stepZ * tez;

            for (int tex = 0; tex < esTarget.nHoriz; tex++)
            {
                float x = esTarget.Rect2.A.X + stepX * tex;

                ElevationPixel epx = erSource.Elevations[tez, tex];

                /*
                 * Height only. ClusterBaseElevationOperator wrote Biome = 1 across the
                 * city rectangle below us, and that still means "this is city" whatever
                 * shape the ground has been given.
                 */
                if (field.TryHeightAt(
                        new Vector2(x, z) - v2Origin, out float wanted, out float influence))
                {
                    epx.Height = StreetHeightField.Blend(epx.Height, wanted, influence);
                }

                esTarget.Elevations[tez, tex] = epx;
            }
        }
    }


    private static void _copy(ElevationSegment erSource, ElevationSegment esTarget)
    {
        for (int tez = 0; tez < esTarget.nVert; tez++)
        {
            for (int tex = 0; tex < esTarget.nHoriz; tex++)
            {
                esTarget.Elevations[tez, tex] = erSource.Elevations[tez, tex];
            }
        }
    }


    /**
     * Build the field once per city, from the whole stroke graph.
     *
     * Whole graph, not the strokes overlapping this fragment: a street just outside the
     * fragment still grades ground inside it, and a per-fragment subset would give two
     * neighbouring fragments different answers along their shared edge.
     *
     * Built outside the lock, for the reason RelaxedStreetHeight builds outside its own:
     * StrokeStore() can trigger a whole city's generation and holding a lock across that
     * would put unrelated work behind it. Two threads racing both compute the same field
     * - it is a pure function of the graph and the relaxed heights - and the first to
     * store wins.
     */
    private StreetHeightField _ensureField()
    {
        lock (_lo)
        {
            if (null != _field) return _field;
        }

        /*
         * StrokeStore() triggers street generation, which samples the layer below this
         * one through TerrainStreetHeight and so cannot come back here. It is re-entrant
         * on the same thread in any case: _triggerStreets_nl returns immediately once the
         * cluster state has left Created.
         */
        var store = _clusterDesc.StrokeStore();
        var source = _clusterDesc.StreetHeightSource;

        var field = StreetHeightField.Build(
            store.GetStrokes(), sp => source.GroundHeightAt(sp), StreetHeightField.DefaultRadius);

        if (0 == field.SpanCount)
        {
            Warning(_dc,
                $"Cluster {_clusterDesc.Name} has no strokes to grade its terrain toward; "
                + "the ground under it is left as the noise produced it.");
        }

        lock (_lo)
        {
            _field ??= field;
            return _field;
        }
    }


    public bool ElevationOperatorIntersects(geom.AABB aabb)
    {
        return aabb.IntersectsXZ(_aabb);
    }


    public ClusterConformElevationOperator(
        in world.ClusterDesc clusterDesc,
        in string strKey
    )
    {
        _clusterDesc = clusterDesc;
        _strKey = strKey;

        _aabb = clusterDesc.AABB;
        _aabb.Extend(StreetHeightField.DefaultRadius);
    }
}
