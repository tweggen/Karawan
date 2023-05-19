using System.Collections.Generic;
using System.Numerics;

namespace engine.world
{
    public class ClusterList
    {
        static private object _lockObject = new();
        static private ClusterList _instance;

        private List<ClusterDesc> _listClusters;


        /**
         * Return the cluster at the given position.
         */
        public ClusterDesc GetClusterAt(in Vector3 pos)
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
        
        
        /**
         * Return a list of clusters.
         * TODO: Make a nonmodifiable list.
         */
        public List<ClusterDesc> GetClusterList()
        {
            return _listClusters;
        }

        /**
         * Add a new cluster to the list of cluster. 
         * To keep everything consistent, this method is responsible for adding
         * the cluster to the catalogue.
         */
        public void AddCluster(ClusterDesc clusterDesc)
        {
            _listClusters.Add(clusterDesc);
        }

        private ClusterList() 
        {
            _listClusters = new List<ClusterDesc>();
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
