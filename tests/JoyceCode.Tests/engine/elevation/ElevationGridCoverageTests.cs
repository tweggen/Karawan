using System;
using System.IO;
using System.Numerics;
using System.Text.RegularExpressions;
using engine.elevation;
using engine.geom;
using engine.world;
using Xunit;

namespace JoyceCode.Tests.engine.elevation;


/**
 * Does the base terrain grid's unwritten last row actually reach anything?
 *
 * nogame.terrain.ElevationBaseFactory fills a fragment's GroundResolution+1 squared grid
 * with
 *
 *     for (int y = y0; y <= y1; y++)
 *         for (int x = x0; x < x1; x++)
 *             elevationSegment.Elevations[x, y] = ...
 *
 * - inclusive in one dimension and exclusive in the other. The write is Elevations[x, y]
 * while every reader indexes [ez, ex], so the index the loop never reaches is the FIRST
 * one, i.e. the last Z row of every fragment, which keeps its default Height of 0.
 *
 * CITY-3D-OPEN-POINTS (h) recorded this as a possible "400 m-pitch cliff grid over the
 * entire world" and asked for it to be verified before being believed. These tests are
 * that verification, and they run the real Cache, the real stitcher and the real
 * CacheEntry rather than a replica of them. The answer is that the hole is real, that the
 * drawn terrain never touches it, and that it reaches exactly one thing: a point query.
 *
 * **On registering into the process-global Cache singleton.** The sibling
 * ClusterConformElevationOperatorTests declines to do this, and it is right to: an
 * operator registered for the whole world leaks into every other test in the assembly.
 * These operators are confined instead. ElevationOperatorIntersects returns true only
 * inside TestArea, four fragments about 280 km from the origin, so
 * Cache._getNextFactoryEntryBelow - which walks down the layer list until it finds one
 * that intersects - can never select them for a query anywhere a real city is. The three
 * layers are queried by explicit layer string, never TOP_LAYER, so registering them also
 * cannot change which layer TOP_LAYER resolves to for anyone else.
 */
public class ElevationGridCoverageTests
{
    private const int Gr = 20;

    /*
     * Four fragments, far enough out that no other test's geometry is near them.
     */
    private const int I0 = 700;
    private const int K0 = 703;

    private const string LayerLeaky = "/700010/gridCoverageLeaky";
    private const string LayerPassThrough = "/700020/gridCoveragePassThrough";
    private const string LayerFixed = "/700030/gridCoverageFixed";

    private const string BelowLeaky = "/700011";
    private const string BelowPassThrough = "/700021";
    private const string BelowFixed = "/700031";

    private static readonly AABB TestArea = new(
        new Vector3(I0 * MetaGen.FragmentSize - MetaGen.FragmentSize, 0f,
            K0 * MetaGen.FragmentSize - MetaGen.FragmentSize),
        new Vector3((I0 + 2) * MetaGen.FragmentSize, 0f,
            (K0 + 2) * MetaGen.FragmentSize));

    private static readonly object _registrationLock = new();
    private static bool _isRegistered;


    /**
     * A terrain height as a function of the GLOBAL grid indices, so that two neighbouring
     * fragments agree exactly on the sample they share: fragment i's index Gr and fragment
     * i+1's index 0 are the same world point and get the same number. That is what makes
     * "did the stitcher take this from the right place" a question with an answer.
     *
     * The amplitude is large on purpose. A hole that reads back as 0 has to be
     * unmistakable next to a real sample.
     */
    private static float H(int gx, int gz)
        => 100f * MathF.Sin(gx * 0.31f)
           + 80f * MathF.Cos(gz * 0.23f)
           + 0.5f * gx - 0.4f * gz
           + 500f;


    private static void _fragmentOf(in Rect2 rect2, out int i, out int k)
    {
        float fs = MetaGen.FragmentSize;
        i = (int)Math.Floor((rect2.A.X + fs / 2.0) / fs);
        k = (int)Math.Floor((rect2.A.Y + fs / 2.0) / fs);
    }


    /**
     * The base grid, written with ElevationBaseFactory's loop shape: the outer loop is
     * inclusive and drives the SECOND index, the inner loop is exclusive and drives the
     * FIRST. Flip writesLastRow to close it.
     */
    private sealed class GridOperator : global::engine.elevation.IOperator
    {
        private readonly bool _writesLastRow;

        public GridOperator(bool writesLastRow) => _writesLastRow = writesLastRow;

        public bool ElevationOperatorIntersects(AABB aabb) => TestArea.IntersectsXZ(aabb);

        public void ElevationOperatorProcess(
            in IElevationProvider elevationInterface,
            in ElevationSegment esTarget)
        {
            _fragmentOf(esTarget.Rect2, out int i, out int k);

            for (int b = 0; b <= Gr; b++)
            {
                for (int a = 0; a < (_writesLastRow ? Gr + 1 : Gr); a++)
                {
                    esTarget.Elevations[a, b] = new ElevationPixel
                    {
                        Height = H(i * Gr + b, k * Gr + a),
                        Biome = 0,
                        Flags1 = 0
                    };
                }
            }
        }
    }


    /**
     * The shape every operator above the base has: read the layer below through
     * GetElevationSegmentBelow over one's own rect, then write every cell of the target.
     * ClusterBaseElevationOperator and ClusterConformElevationOperator both do exactly
     * this, which is the whole question in the "does a city heal the hole" test.
     */
    private sealed class PassThroughOperator : global::engine.elevation.IOperator
    {
        public bool ElevationOperatorIntersects(AABB aabb) => TestArea.IntersectsXZ(aabb);

        public void ElevationOperatorProcess(
            in IElevationProvider elevationInterface,
            in ElevationSegment esTarget)
        {
            var erSource = elevationInterface.GetElevationSegmentBelow(esTarget.Rect2);
            for (int tez = 0; tez < esTarget.nVert; tez++)
            {
                for (int tex = 0; tex < esTarget.nHoriz; tex++)
                {
                    esTarget.Elevations[tez, tex] = erSource.Elevations[tez, tex];
                }
            }
        }
    }


    private static Cache _cache()
    {
        var cache = Cache.Instance();
        lock (_registrationLock)
        {
            if (!_isRegistered)
            {
                cache.ElevationCacheRegisterElevationOperator(LayerLeaky, new GridOperator(false));
                cache.ElevationCacheRegisterElevationOperator(LayerPassThrough, new PassThroughOperator());
                cache.ElevationCacheRegisterElevationOperator(LayerFixed, new GridOperator(true));
                _isRegistered = true;
            }
        }

        return cache;
    }


    /**
     * The hole is real: the last Z row of a base fragment is never written and reads back
     * as the default 0, against a field that is nowhere near 0.
     */
    [Fact]
    public void TheBaseGridLeavesItsLastZRowUnwritten()
    {
        var entry = _cache().ElevationCacheGetBelow(I0, K0, BelowLeaky);

        for (int ex = 0; ex <= Gr; ++ex)
        {
            Assert.Equal(0f, entry.elevations[Gr, ex].Height);
            Assert.True(H(I0 * Gr + ex, K0 * Gr + Gr) > 100f,
                "the field itself must be far from zero here, or the assertion above is vacuous");
        }

        /*
         * Everything else is written, so this is a missing row and not a missing grid.
         */
        for (int ez = 0; ez < Gr; ++ez)
        {
            for (int ex = 0; ex <= Gr; ++ex)
            {
                Assert.Equal(H(I0 * Gr + ex, K0 * Gr + ez), entry.elevations[ez, ex].Height, 3);
            }
        }
    }


    /**
     * ...and the drawn terrain never reads it.
     *
     * CreateTerrainOperator hands TerrainKnitter the segment from
     * ElevationCacheGetRectBelow over the fragment's own rect. That stitcher copies
     * global elevation indices k*Gr .. (k+1)*Gr-1 out of fragment k - local 0..Gr-1, never
     * local Gr - and takes the boundary sample from fragment k+1's local index 0 instead.
     * So the mesh is built entirely out of written cells.
     *
     * This is the measurement that refutes CITY-3D-OPEN-POINTS (h)'s "400 m-pitch cliff
     * grid over the whole world": there is no cliff in the terrain that is drawn.
     */
    [Fact]
    public void TheStitchedGridTheTerrainMeshReadsIsComplete()
    {
        MetaGen.GetFragmentRect(I0, K0, out var rect2);
        var segment = _cache().ElevationCacheGetRectBelow(rect2, BelowLeaky);

        Assert.Equal(Gr + 1, segment.nHoriz);
        Assert.Equal(Gr + 1, segment.nVert);

        for (int ez = 0; ez <= Gr; ++ez)
        {
            for (int ex = 0; ex <= Gr; ++ex)
            {
                Assert.Equal(H(I0 * Gr + ex, K0 * Gr + ez), segment.Elevations[ez, ex].Height, 3);
            }
        }
    }


    /**
     * The one thing that DOES read it is a point query.
     *
     * CacheEntry.GetElevationPixelAt reads elevations[ey+1, ex] for the far corner of the
     * cell it lands in, so a position in the last 20 m strip of a fragment - ey = Gr-1 -
     * interpolates between a real sample and the unwritten 0. That is
     * Loader.GetHeightAt / GetElevationPixelAt, i.e. ClusterDesc.GroundHeightAt, the hover
     * probe's terrain fallback, GetWalkingHeightAt and debris placement.
     *
     * Asserted as the gap between the leaky grid and the same field with the row written,
     * so the expected value comes from the engine's own interpolation and not from a
     * second implementation of it.
     */
    [Fact]
    public void APointQueryFallsIntoTheUnwrittenRow()
    {
        var cache = _cache();
        var leaky = cache.ElevationCacheGetBelow(I0, K0, BelowLeaky);
        var whole = cache.ElevationCacheGetBelow(I0, K0, BelowFixed);

        float fs = MetaGen.FragmentSize;
        float step = fs / Gr;

        /*
         * Well inside the fragment the two agree exactly.
         */
        for (int ez = 0; ez < Gr - 1; ++ez)
        {
            var v3 = new Vector3(-fs / 2f + 7f, 0f, -fs / 2f + ez * step + 7f);
            Assert.Equal(
                whole.GetElevationPixelAt(v3).Height,
                leaky.GetElevationPixelAt(v3).Height, 3);
        }

        /*
         * In the last strip they part company, and by a lot.
         */
        float worst = 0f;
        for (int ex = 0; ex <= Gr - 1; ++ex)
        {
            var v3 = new Vector3(
                -fs / 2f + ex * step + 1f, 0f,
                -fs / 2f + (Gr - 1) * step + 19f);
            float delta = MathF.Abs(
                whole.GetElevationPixelAt(v3).Height - leaky.GetElevationPixelAt(v3).Height);
            worst = MathF.Max(worst, delta);
        }

        Assert.True(worst > 100f,
            $"a point query in the last 20 m strip must collapse toward zero; worst gap was {worst} m");
    }


    /**
     * An operator above the base heals the hole, which is why no city ever showed it.
     *
     * The pass-through here is the shape ClusterBaseElevationOperator and
     * ClusterConformElevationOperator both have: they fill their whole target from
     * GetElevationSegmentBelow, which is the stitcher, which is complete. So inside a
     * cluster - flat or terrain-following - the last row is written from the neighbouring
     * fragment and the point query is correct. The defect survives only on fragments where
     * the base layer IS the top layer, i.e. outside every city.
     */
    [Fact]
    public void AnOperatorAboveTheBaseHealsTheHole()
    {
        var entry = _cache().ElevationCacheGetBelow(I0, K0, BelowPassThrough);

        for (int ez = 0; ez <= Gr; ++ez)
        {
            for (int ex = 0; ex <= Gr; ++ex)
            {
                Assert.Equal(H(I0 * Gr + ex, K0 * Gr + ez), entry.elevations[ez, ex].Height, 3);
            }
        }
    }


    /**
     * The fix, stated where it lives.
     *
     * ElevationBaseFactory is in nogameCode, which this assembly does not reference, so
     * the loop bounds are scanned for rather than executed. Without this the tests above
     * describe a defect that the shipped operator is free to keep.
     */
    [Fact]
    public void TheBaseFactoryWritesBothLastIndices()
    {
        string path = Path.Combine(
            _repoRoot(), "nogameCode", "nogame", "terrain", "ElevationBaseFactory.cs");
        Assert.True(File.Exists(path), $"expected the base elevation factory at {path}");

        string source = File.ReadAllText(path);

        var mFor = Regex.Matches(source, @"for\s*\(\s*int\s+(\w+)\s*=\s*\w+\s*;\s*\1\s*(<=?)\s*(\w+)\s*;");
        Assert.True(mFor.Count >= 2,
            "expected the grid copy loop to still be two nested for statements");

        foreach (Match m in mFor)
        {
            if (m.Groups[3].Value is not ("x1" or "y1"))
            {
                continue;
            }

            Assert.True(m.Groups[2].Value == "<=",
                $"the copy loop over {m.Groups[3].Value} must be inclusive, or the last "
                + "row/column of every fragment's elevation grid stays at its default "
                + "height of zero and every point query in that strip reads it");
        }
    }


    private static string _repoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Karawan.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
