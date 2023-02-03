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
            // trace('ClusterBaseElevationOperator(): Operating.');
            // var fs = WorldMetaGens.fragmentSize;

            float x0 = _clusterDesc.Pos.X - _clusterDesc.Size / 2f;
            float z0 = _clusterDesc.Pos.Z - _clusterDesc.Size / 2f;
            float x1 = _clusterDesc.Pos.X + _clusterDesc.Size / 2f;
            float z1 = _clusterDesc.Pos.Z + _clusterDesc.Size / 2f;

            // TXWTODO: Cache this in the cluster?
            Rect erCluster = elevationInterface.GetElevationRectBelow(x0, z0, x1, z1);
            float aver = 0f;
            for(int cez=0; cez < erCluster.nVert; ++cez ) {
                for(int cex=0; cex<erCluster.nHoriz; ++cex ) {
                    aver += erCluster.Elevations[cez,cex];
                }
            }

            aver /= erCluster.nHoriz * erCluster.nVert;
            _clusterDesc.AverageHeight = aver;

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

            /*
             * Copy data from source to target, modifying it.
             *
             * In this version we only apply the change to values within our
             * city bounds.
             */
            for (int tez=0; tez<erTarget.nVert; tez++ )
            {
                var z = erTarget.Z0
                    + ((erTarget.Z1 - erTarget.Z0) * tez)
                        / erTarget.nVert;

                for (int tex=0; tex<erTarget.nHoriz; tex++ )
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
                    float sourceHeight = erSource.Elevations[tez,tex];

                    if (true
                        && x >= x0
                        && x <= x1
                        && z >= z0
                        && z <= z1
                    )
                    {
                        /*
                         * This is wihtin the range of our city. 
                         * So flatten it.
                         * 
                         * Just use one plain elevation, we cannot deal yet with
                         * different levels.
                         */
                        if (sourceHeight < aver)
                        {
                            // resultHeight = aver - 0 ;
                            resultHeight = aver + 1.5f;
                        }
                        else
                        {
                            resultHeight = aver + 1.5f;
                        }
                    }
                    else
                    {
                        /*
                         * This is not within the range of our city.
                         */
                        resultHeight = sourceHeight;
                    }
                    erTarget.Elevations[tez,tex] = resultHeight;
                }
            }
        }


        public bool ElevationOperatorIntersects(
            float x0, float z0,
            float x1, float z1)
        {
            var sh = _clusterDesc.Size / 2.0;
            var cx0 = _clusterDesc.Pos.X - sh;
            var cz0 = _clusterDesc.Pos.Z - sh;
            var cx1 = _clusterDesc.Pos.X + sh;
            var cz1 = _clusterDesc.Pos.Z + sh;
            if (
                false
                || x0 > cx1
                || z0 > cz1
                || x1 < cx0
                || z1 < cz0
            )
            {
                return false;
            }
            return true;
        }


        public ClusterBaseElevationOperator(
            in world.ClusterDesc clusterDesc,
            in string strKey
        )
        {
            _clusterDesc = clusterDesc;
            _strKey = strKey;
            // _rnd = new engine.RandomSource(strKey);
        }


    }
}
