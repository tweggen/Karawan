using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;
using Octree;

namespace engine.world
{
    public class ClusterList
    {
        static private object _lockObject = new();
        static private ClusterList _instance;

        private object _lo = new();
        
        private List<ClusterDesc> _listClusters;

        private Octree.BoundsOctree<ClusterDesc> _octreeClusters;

        /**
         * Return the cluster at the given position.
         */
        public ClusterDesc GetClusterAt(in Vector3 pos)
        {
            lock (_lo)
            {
                foreach (ClusterDesc cluster in _listClusters)
                {
                    if (cluster.IsInside(pos))
                    {
                        return cluster;
                    }
                }

                return null;
            }
        }


        private List<ClusterDesc> _listColliding = new ();
        public List<ClusterDesc> IntersectsCluster(in Vector3 va, in Vector3 vb)
        {
            lock (_lo)
            {
                Vector3 vab = vb - va;
                if (!_octreeClusters.GetCollidingNonAlloc(_listColliding, new Ray(va, vab), vab.Length()))
                {
                    return null;
                }

                var list = _listColliding;
                _listColliding = new();
                return list;
            }
        }


        /**
         * Return a list of clusters.
         * TODO: Make a nonmodifiable list.
         */
        public ReadOnlyCollection<ClusterDesc> _roList = null;
        public ReadOnlyCollection<ClusterDesc> GetClusterList()
        {
            lock (_lo)
            {
                if (null == _roList)
                {
                    _roList = new(_listClusters);
                }

                return _roList;
            }
        }

        /**
         * Add a new cluster to the list of cluster. 
         * To keep everything consistent, this method is responsible for adding
         * the cluster to the catalogue.
         */
        public void AddCluster(ClusterDesc clusterDesc)
        {
            lock (_lo)
            {
                _listClusters.Add(clusterDesc);
                _octreeClusters.Add(clusterDesc,
                    new BoundingBox(
                        clusterDesc.Pos,
                        new Vector3(1f, 1f, 1f) * clusterDesc.Size
                    )
                );
            }
        }

        private ClusterList() 
        {
            _listClusters = new List<ClusterDesc>();
            _octreeClusters = new( MetaGen.MaxWidth, Vector3.Zero, 100f, 1.2f);
        }

        public static ClusterList Instance()
        {
            lock( _lockObject)
            {
                if( null==_instance )
                {
                    _instance = new ClusterList();
                }
                return _instance;
            }
        }
    }
}
