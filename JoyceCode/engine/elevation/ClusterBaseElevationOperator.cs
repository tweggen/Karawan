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
            in ElevationSegment esTarget
        )
        {
            ElevationSegment erCluster = elevationInterface.GetElevationSegmentBelow(
                _clusterDesc.Rect2);
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

                    float resultHeight;
                    float sourceHeight = erSource.Elevations[tez,tex];

                    if (_clusterDesc.Rect2.Contains(x, z))
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
                    esTarget.Elevations[tez,tex] = resultHeight;
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
            // _rnd = new engine.RandomSource(strKey);
        }


    }
}
