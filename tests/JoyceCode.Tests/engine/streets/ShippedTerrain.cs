using System;
using System.Collections.Generic;
using System.Numerics;
using engine.streets;
using engine.world;

namespace JoyceCode.Tests.engine.streets;


/**
 * The terrain the game actually ships, sampled from a test.
 *
 * nogame.terrain.GroundOperator and nogame.terrain.ElevationBaseFactory live in
 * nogameCode, which the test assembly deliberately does not reference - but everything
 * they are BUILT from is in Joyce: builtin.tools.RandomSource, the diamond-square in
 * engine.elevation.Tools.RefineSkeletonElevation, and the world constants in
 * engine.world.MetaGen. So this reproduces them term for term rather than approximating
 * them with a sine field:
 *
 *   - a world skeleton of one sample per FragmentSize (400 m), seeded "mydear", corners
 *     drawn from the same four RandomSource calls in the same order, refined by
 *     RefineSkeletonElevation over MetaGen.MaxSize;
 *   - per fragment, a GroundResolution+1 square (21x21, i.e. one sample every 20 m)
 *     whose four corners come from the skeleton, refined again over FragmentSize2 with
 *     the fragment's own (idxX, idxY) as the noise seed - which is what makes the
 *     refinement deterministic per fragment and continuous across fragment boundaries;
 *   - the same two-triangle interpolation CacheEntry.GetElevationPixelAt uses within a
 *     cell, so a point query answers what the game's point query answers.
 *
 * Not reproduced, and deliberately: ClusterBaseElevationOperator (which would flatten
 * the city away) and ClusterConformElevationOperator (which grades the ground TOWARD the
 * streets, and so cannot affect a street height - the conforming pass reads the street
 * graph, never the other way round). What a block floor is built from is
 * IStreetHeightSource.GroundHeightAt at the block's own junctions, and that samples the
 * layer BELOW the conforming pass. This is that layer.
 */
internal static class ShippedTerrain
{
    private const float MinElevation = -10f;
    private const float MaxElevation = 100f;

    private static readonly object _lo = new();
    private static float[,] _skeleton;
    private static readonly Dictionary<long, float[,]> _fragments = new();


    /**
     * The world skeleton, exactly as GroundOperator's constructor builds it.
     */
    private static float[,] _skeletonOf()
    {
        lock (_lo)
        {
            if (null != _skeleton)
            {
                return _skeleton;
            }

            float fragmentSize = MetaGen.FragmentSize;
            int w = (int)((MetaGen.MaxWidth + fragmentSize - 1) / fragmentSize) + 1;
            int h = (int)((MetaGen.MaxHeight + fragmentSize - 1) / fragmentSize) + 1;

            var rnd = new global::builtin.tools.RandomSource("mydear");
            rnd.Clear();

            var sk = new float[h, w];
            float amplitude = MaxElevation - MinElevation;
            float bias = MinElevation;

            sk[0, 0] = rnd.GetFloat() * amplitude + bias;
            sk[0, w - 1] = rnd.GetFloat() * amplitude + bias;
            sk[h - 1, 0] = rnd.GetFloat() * amplitude + bias;
            sk[h - 1, w - 1] = rnd.GetFloat() * amplitude + bias;

            global::engine.elevation.Tools.RefineSkeletonElevation(
                0, 0, sk, MinElevation, MaxElevation,
                0, 0, w - 1, h - 1, MetaGen.MaxSize);

            _skeleton = sk;
            return _skeleton;
        }
    }


    /**
     * One fragment's elevation grid, exactly as ElevationBaseFactory fills it.
     */
    private static float[,] _fragmentOf(int i, int k)
    {
        long key = ((long)i << 32) ^ (uint)k;

        lock (_lo)
        {
            if (_fragments.TryGetValue(key, out var cached))
            {
                return cached;
            }
        }

        var sk = _skeletonOf();

        int n = MetaGen.GroundResolution + 1;
        int idxX = (int)((i * MetaGen.FragmentSize + MetaGen.MaxWidth / 2.0) / MetaGen.FragmentSize);
        int idxY = (int)((k * MetaGen.FragmentSize + MetaGen.MaxHeight / 2.0) / MetaGen.FragmentSize);

        var local = new float[n, n];
        local[0, 0] = sk[idxY, idxX];
        local[0, n - 1] = sk[idxY, idxX + 1];
        local[n - 1, 0] = sk[idxY + 1, idxX];
        local[n - 1, n - 1] = sk[idxY + 1, idxX + 1];

        global::engine.elevation.Tools.RefineSkeletonElevation(
            idxX, idxY, local, MinElevation, MaxElevation,
            0, 0, n - 1, n - 1, MetaGen.FragmentSize2);

        lock (_lo)
        {
            _fragments[key] = local;
        }

        return local;
    }


    /**
     * The height of the shipped terrain at a world position.
     */
    public static float HeightAt(float x, float z)
    {
        float fs = MetaGen.FragmentSize;
        int i = (int)Single.Floor((x + fs / 2f) / fs);
        int k = (int)Single.Floor((z + fs / 2f) / fs);

        var grid = _fragmentOf(i, k);

        float lx = x - i * fs;
        float lz = z - k * fs;

        float step = fs / MetaGen.GroundResolution;
        int ex = (int)((lx + fs / 2f) / step);
        int ey = (int)((lz + fs / 2f) / step);

        ex = Math.Clamp(ex, 0, MetaGen.GroundResolution - 1);
        ey = Math.Clamp(ey, 0, MetaGen.GroundResolution - 1);

        float y00 = grid[ey, ex];
        float y01 = grid[ey, ex + 1];
        float y10 = grid[ey + 1, ex];
        float y11 = grid[ey + 1, ex + 1];

        float tx = (lx + fs / 2f) - step * ex;
        float ty = (lz + fs / 2f) - step * ey;

        if (tx + ty <= step)
        {
            return y00 + (y01 - y00) * tx / step + (y10 - y00) * ty / step;
        }

        return y11 + (y10 - y11) * (step - tx) / step + (y01 - y11) * (step - ty) / step;
    }


    /**
     * A cluster's street heights on the shipped terrain, with the unbuildable gradients
     * taken out - i.e. what RelaxedStreetHeight over TerrainStreetHeight answers in the
     * game, reached without the elevation cache or the I container.
     *
     * RelaxedStreetHeight itself calls ClusterDesc.StrokeStore(), which needs
     * ClusterStorage registered, so the same two steps are done here directly against
     * the same GradeRelaxer and the same shipped GradePolicy.
     */
    public static IStreetHeightSource StreetHeightsOf(
        ClusterDesc clusterDesc, StrokeStore strokeStore)
    {
        var heights = new Dictionary<int, float>();
        foreach (var sp in strokeStore.GetStreetPoints())
        {
            heights[sp.Id] = HeightAt(
                clusterDesc.Pos.X + sp.Pos.X, clusterDesc.Pos.Z + sp.Pos.Y);
        }

        GradeRelaxer.Relax(strokeStore.GetStrokes(), heights, new GradePolicy());

        return new TableStreetHeight(heights);
    }


    /**
     * A height source that answers from a table of junction ids.
     */
    private sealed class TableStreetHeight : IStreetHeightSource
    {
        private readonly Dictionary<int, float> _heights;

        public bool IsFlat => false;

        public float GroundHeightAt(StreetPoint sp)
            => _heights.TryGetValue(sp.Id, out float h) ? h : 0f;

        public TableStreetHeight(Dictionary<int, float> heights)
        {
            _heights = heights;
        }
    }
}
