using System;
using builtin.tools;
using engine.world;

namespace engine.streets;


/**
 * Creates the initial "highway trigger" strokes that seed street generation for a
 * cluster, and applies the generator's cluster bounds.
 *
 * Extracted verbatim from ClusterDesc._addHighwayTriggers / _generateStrokes so that
 * the game and the deterministic street-generation test harness seed the generator
 * through exactly the same code path. Without this, the harness would only ever test
 * a lookalike of the real seeding.
 *
 * WARNING: the order and count of RandomSource draws below is part of the generated
 * street layout. Reordering, adding or removing a draw changes every cluster in the
 * game. See docs/roadmap/proposed/STREETS-GENERATOR-REWORK-PLAN.md section 0.2.
 */
internal static class StreetSeeds
{
    internal const float InnerStreetWeight = 1.1f;
    internal const float OuterStreetWeight = 1.2f;


    /**
     * Restrict generation to the cluster area, inset by one terrain facet.
     */
    internal static void ApplyBounds(Generator streetGenerator, ClusterDesc clusterDesc)
    {
        float size = clusterDesc.Size;
        float terrainFacetSize =
            world.MetaGen.FragmentSize / (float) world.MetaGen.GroundResolution;
        streetGenerator.SetBounds(
            -size / 2f + terrainFacetSize,
            -size / 2f + terrainFacetSize,
            size / 2f - terrainFacetSize,
            size / 2f - terrainFacetSize);
    }


    /**
     * Using the information about the next cities, create seed points for
     * the map based on the interconnecting stations.
     */
    internal static void AddTo(
        Generator streetGenerator,
        ClusterDesc clusterDesc,
        RandomSource rnd)
    {
        float size = clusterDesc.Size;

        float initialOuterStreetLength =
            Single.Max(45f, Single.Min(1000f, size) / 12f);
        float initialInnerStreetLength =
            Single.Max(45f, Single.Min(1000f, size) / 16f);

        /*
         * Variant two: n random points
         */
        var nSeeds = (rnd.Get8()>>5)+1;
        for( int i=0; i<nSeeds; ++i ) {
            engine.streets.StreetPoint newA = new engine.streets.StreetPoint() { ClusterId = clusterDesc.Id };
            float x = rnd.Get8()*((2f*size)/3f)/256f-size/3f;
            float y = rnd.Get8()*((2f*size)/3f)/256f-size/3f;
            newA.SetPos( x, y );
            float dir = rnd.Get8()*(float)Math.PI/128f;
            var newB = new engine.streets.StreetPoint() { ClusterId = clusterDesc.Id };
            var stroke = engine.streets.Stroke.CreateByAngleFrom( clusterDesc,
                newA, newB, dir,
                initialInnerStreetLength, true, InnerStreetWeight );
            streetGenerator.AddStartingStroke(stroke);
        }

        /*
         * Plus the four corners.
         */
        {
            var newA = new StreetPoint() { ClusterId = clusterDesc.Id };
            newA.SetPos( -size/2.2f, -size/2.2f );
            var newB = new StreetPoint() { ClusterId = clusterDesc.Id };
            var stroke = Stroke.CreateByAngleFrom( clusterDesc,
                newA, newB, (float)Math.PI*0.25f,
                initialOuterStreetLength, true, OuterStreetWeight );
            streetGenerator.AddStartingStroke(stroke);
        }
        {
            var newA = new StreetPoint() { ClusterId = clusterDesc.Id };
            newA.SetPos( size/2.1f, -size/2.1f );
            var newB = new StreetPoint() { ClusterId = clusterDesc.Id };
            var stroke = Stroke.CreateByAngleFrom( clusterDesc,
                newA, newB, 3f*(float)Math.PI*0.25f,
                initialOuterStreetLength, true, OuterStreetWeight );
            streetGenerator.AddStartingStroke(stroke);
        }
        {
            var newA = new StreetPoint() { ClusterId = clusterDesc.Id };
            newA.SetPos( -size/2.2f, size/2.2f );
            var newB = new StreetPoint() { ClusterId = clusterDesc.Id };
            var stroke = Stroke.CreateByAngleFrom( clusterDesc,
                newA, newB, -(float)Math.PI*0.25f,
                initialOuterStreetLength, true, OuterStreetWeight );
            streetGenerator.AddStartingStroke(stroke);
        }
        {
            var newA = new StreetPoint() { ClusterId = clusterDesc.Id };
            newA.SetPos( size/2.15f, size/2.2f );
            var newB = new StreetPoint() { ClusterId = clusterDesc.Id };
            var stroke = Stroke.CreateByAngleFrom( clusterDesc,
                newA, newB, -3.0f*(float)Math.PI*0.25f,
                initialOuterStreetLength, true, OuterStreetWeight );
            streetGenerator.AddStartingStroke(stroke);
        }
    }
}
