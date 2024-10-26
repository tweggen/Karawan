using System.Collections.Generic;
using System.Numerics;
using engine.world;
using Octree;

namespace engine.streets
{
    public class QuarterStore
    {
        private List<Quarter> _listQuarters;
        private ClusterDesc _clusterDesc;
        private Octree.BoundsOctree<Quarter> _octreeQuarters;


        
        public streets.Quarter GuessQuarter(in Vector2 p)
        {
            var pc = p;
            pc.X -= _clusterDesc.Pos.X;
            pc.Y -= _clusterDesc.Pos.Z;

            List<Quarter> tmpQuarterList = new ();
            if (!_octreeQuarters.GetCollidingNonAlloc(tmpQuarterList,
                    new BoundingBox(new Vector3(pc.X, 0f, pc.Y), Vector3.One*0.05f)))
            {
                /*
                 * Fast wrong implementation: Find the closest center point.
                 */
                float minDist = 9999999999f;
                streets.Quarter minQuarter = null;
                foreach(var quarter in tmpQuarterList )
                {
                    var cp = quarter.GetCenterPoint();
                    float dist = Vector2.Distance( cp, pc );
                    if (dist < minDist)
                    {
                        minDist = dist;
                        minQuarter = quarter;
                    }
                }
                return minQuarter;
            }

            return null;
#if false
            /*
             * Fast wrong implementation: Find the closest center point.
             */
            var pc = p;
            pc.X -= _clusterDesc.Pos.X;
            pc.Y -= _clusterDesc.Pos.Z;
            float minDist = 9999999999f;
            streets.Quarter minQuarter = null;
            foreach(var quarter in _listQuarters )
            {
                var cp = quarter.GetCenterPoint();
                float dist = Vector2.Distance( cp, pc );
                if (dist < minDist)
                {
                    minDist = dist;
                    minQuarter = quarter;
                }
            }
            return minQuarter;
#endif
        }


        public void Add(in Quarter quarter)
        {
            
            var bb = new Octree.BoundingBox(quarter.AABB.Center, (2f * quarter.AABB.BB - quarter.AABB.Center));

            _octreeQuarters.Add(quarter, bb);
            _listQuarters.Add(quarter);
        }

        public List<Quarter> GetQuarters()
        {
            return _listQuarters;
        }

        public QuarterStore(ClusterDesc clusterDesc)
        {
            _clusterDesc = clusterDesc;
            _listQuarters = new List<Quarter>();
            _octreeQuarters = new(clusterDesc.Size, Vector3.Zero, 5f, 1f);
        }
    }
}
