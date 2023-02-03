using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace engine.world
{
    public class ClusterList
    {
        static private object _lockObject = new();
        static private ClusterList _instance;

        private List<ClusterDesc> _listClusters;

        /**
         * Return a list of clusters.
         * TODO: Make a nonmodifiable list.
         */
        public List<ClusterDesc> getClusterList()
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
