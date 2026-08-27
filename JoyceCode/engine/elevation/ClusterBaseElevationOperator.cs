using System;
using System.Collections.Generic;
using System.Text;

namespace engine.elevation
{
    public class ClusterBaseElevationOperator : IOperator
    {

        /**
         * This is the city cluster I am associated with.
         */
        private world.ClusterDesc _clusterDesc;

        private string _strKey;

        /**
         * Computes a city's average ground height, and - unless told otherwise -
         * irons the whole cluster rectangle flat to it.
         *
         * The two jobs are joined at the hip and must not be separated by deleting
         * this operator: ClusterDesc.AverageHeight is computed HERE, and nearly thirty
         * sites across streets, quarters, buildings, navigation and TALE read it as
         * "the height of the city". Unwiring the operator would leave every city at
         * zero. So the flattening is skipped by a flag while the average is still
         * produced.
         *
         * With joyce.DisableClusterFlattening=true the terrain keeps its shape and
         * streets follow it (see engine.streets.TerrainStreetHeight). Everything else
         * in the city still sits at the average, which is the point of the flag: it
         * makes that visible one subsystem at a time rather than all at once.
         */
        public void ElevationOperatorProcess(
            in IElevationProvider elevationInterface,
            in ElevationSegment esTarget
        )
        {
            ElevationSegment erCluster = elevationInterface.GetElevationSegmentBelow(
                _clusterDesc.Rect2);
            float aver = 0f;
            for(int cez=0; cez < erCluster.nVert; ++cez ) {
                for(int cex=0; cex<erCluster.nHoriz; ++cex ) {
                    aver += erCluster.Elevations[cez,cex].Height;
                }
            }

            aver /= erCluster.nHoriz * erCluster.nVert;
            _clusterDesc.AverageHeight = aver;

            /*
             * Read once rather than per pixel: this runs for every elevation segment
             * the city touches, and the setting cannot change under us mid-city
             * without the result being a city that is half flattened.
             */
            bool keepTerrain =
                engine.GlobalSettings.Get(
                    streets.StreetHeightSources.DisableClusterFlatteningSetting) == "true";

            /*
             * Now that we have the average, read the level below us.
             */
            var erSource = elevationInterface.GetElevationSegmentBelow(
                esTarget.Rect2);

            /*
             * Now we iterate through our target, filling it with the
             * data from source, modifying the data along the way.
             */

            /*
             * Copy data from source to target, modifying it.
             *
             * In this version we only apply the change to values within our
             * city bounds.
             */
            for (int tez=0; tez<esTarget.nVert; tez++ )
            {
                var z = esTarget.Rect2.A.Y
                    + ((esTarget.Rect2.B.Y - esTarget.Rect2.A.Y) * tez)
                        / esTarget.nVert;

                for (int tex=0; tex<esTarget.nHoriz; tex++ )
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

                    var epxSource = erSource.Elevations[tez,tex];
                    var epxDest = epxSource;

                    if (_clusterDesc.Rect2.Contains(x, z))
                    {
                        // TXWTODO: Define this somewhere.
                        epxDest.Biome = 1;

                        /*
                         * This is within the range of our city.
                         *
                         * The biome above is still applied when the terrain is kept:
                         * it says "this is city", which stays true whatever shape the
                         * ground has. Only the height is left alone.
                         */
                        if (!keepTerrain)
                        {
                            /*
                             * Flatten it. Just use one plain elevation, we cannot deal
                             * yet with different levels.
                             */
                            epxDest.Height = aver + 1.5f;
                        }
                    }

                    esTarget.Elevations[tez,tex] = epxDest;
                }
            }
        }


        public bool ElevationOperatorIntersects(engine.geom.AABB aabb)
        {
            return aabb.IntersectsXZ(_clusterDesc.AABB);
        }


        public ClusterBaseElevationOperator(
            in world.ClusterDesc clusterDesc,
            in string strKey
        )
        {
            _clusterDesc = clusterDesc;
            _strKey = strKey;
            // _rnd = new builtin.tools.RandomSource(strKey);
        }


    }
}
