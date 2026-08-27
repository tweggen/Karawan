using System;
using System.Collections.Generic;
using engine;
using JoyceCode.Tests.engine;
using engine.joyce;
using engine.streets;
using engine.world;

namespace JoyceCode.Tests.engine.streets;


/**
 * Runs the street geometry operator without booting an engine.
 *
 * The two emission methods used to take a world.Fragment, but only ever to ask whether
 * the thing being built belonged to that fragment. That decision is the caller's, and
 * hoisting it out left the geometry itself dependent on nothing but a cluster, a
 * stroke store and a material - which is what makes this harness possible at all.
 */
internal static class StreetGeometryHarness
{
    private const string StreetMaterialName = "engine.streets.materials.street";

    private static readonly object _lo = new();
    private static bool _materialReady;


    /**
     * The operator only reads the material for its texture size, which drives UV
     * projection. A stand-in of a known size keeps the harness off the real texture
     * catalogue, and therefore off the asset pipeline.
     */
    private static void _ensureMaterial()
    {
        lock (_lo)
        {
            if (_materialReady) return;

            /*
             * No engine boot here, so the registry the operator asks for has to be put
             * in the container first - but the Assimp fixture needs the same one, and
             * the container is process-global, so this goes through the shared
             * idempotent path rather than claiming it.
             */
            TestContainer.EnsureRegistered<ObjectRegistry<Material>>(
                () => new ObjectRegistry<Material>());

            var registry = I.Get<ObjectRegistry<Material>>();
            registry.Add(StreetMaterialName, new Material()
            {
                Texture = new Texture("test-streets.png") { Width = 1024, Height = 1024 }
            });

            _materialReady = true;
        }
    }


    /**
     * Build the geometry of a whole generated cluster, as the operator would for a
     * fragment large enough to contain all of it.
     *
     * @returns the accumulated mesh.
     */
    internal static Mesh Generate(string idString, float size) => GenerateAtLevel(idString, size, 0);


    /**
     * Build the geometry of a hand-made network, so that a test can set up a structure
     * the generator does not yet produce - a ramp, for instance.
     *
     * @param only
     *     Emit only these strokes. The whole store is still used for the junctions, so
     *     the angle arrays a surface needs are populated.
     */
    internal static Mesh GenerateFor(
        ClusterDesc clusterDesc, StrokeStore strokeStore, IEnumerable<Stroke> only)
    {
        _ensureMaterial();

        var op = new GenerateClusterStreetsOperator(clusterDesc, "geometry-harness");
        var artefact = new Artefact()
        {
            g = Mesh.CreateNormalsListInstance("geometry-harness-explicit")
        };

        foreach (var stroke in only)
        {
            op._generateStreetRun(0f, 0f, stroke, artefact);
        }

        return artefact.g;
    }


    /**
     * As Generate, but against a caller-supplied cluster - so that a test can install a
     * height source on it first and see what the same network looks like over
     * non-flat ground.
     */
    internal static Mesh GenerateWith(ClusterDesc clusterDesc, string idString, float size)
    {
        _ensureMaterial();

        var strokeStore = StreetHarness.Generate(idString, size);
        var op = new GenerateClusterStreetsOperator(clusterDesc, "geometry-harness");
        var artefact = new Artefact()
        {
            g = Mesh.CreateNormalsListInstance("geometry-harness")
        };

        foreach (var stroke in strokeStore.GetStrokes())
        {
            op._generateStreetRun(0f, 0f, stroke, artefact);
        }

        foreach (var streetPoint in strokeStore.GetStreetPoints())
        {
            op._generateJunction(0f, 0f, streetPoint, artefact);
        }

        return artefact.g;
    }


    /**
     * Emit only the caps of the given junctions, so that a test can compare a junction
     * against the roads running into it rather than against a mesh containing both.
     */
    internal static Mesh GenerateJunctionsFor(
        ClusterDesc clusterDesc, StrokeStore strokeStore, IEnumerable<StreetPoint> only)
    {
        _ensureMaterial();

        var op = new GenerateClusterStreetsOperator(clusterDesc, "geometry-harness");
        var artefact = new Artefact()
        {
            g = Mesh.CreateNormalsListInstance("geometry-harness-junctions")
        };

        foreach (var sp in only)
        {
            op._generateJunction(0f, 0f, sp, artefact);
        }

        return artefact.g;
    }


    /**
     * As Generate, but with the whole network moved onto one raised deck.
     *
     * Raising an already generated network rather than generating a multilayer one
     * keeps the plan geometry identical, so a comparison against the ground version
     * isolates elevation and nothing else.
     */
    internal static Mesh GenerateAtLevel(string idString, float size, sbyte level)
    {
        _ensureMaterial();

        var clusterDesc = StreetHarness.MakeCluster(idString, size);
        var strokeStore = StreetHarness.Generate(idString, size);

        if (0 != level)
        {
            foreach (var sp in strokeStore.GetStreetPoints()) sp.Level = level;
            foreach (var stroke in strokeStore.GetStrokes()) stroke.Level = level;
        }

        var op = new GenerateClusterStreetsOperator(clusterDesc, "geometry-harness");

        var artefact = new Artefact()
        {
            g = Mesh.CreateNormalsListInstance("geometry-harness")
        };

        /*
         * Cluster-relative coordinates, i.e. what the operator computes as
         * clusterDesc.Pos - worldFragment.Position for a fragment centred on the
         * cluster.
         */
        float cx = 0f;
        float cy = 0f;

        foreach (var stroke in strokeStore.GetStrokes())
        {
            op._generateStreetRun(cx, cy, stroke, artefact);
        }

        foreach (var streetPoint in strokeStore.GetStreetPoints())
        {
            op._generateJunction(cx, cy, streetPoint, artefact);
        }

        return artefact.g;
    }
}
