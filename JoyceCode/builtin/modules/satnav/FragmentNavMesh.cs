using System;
using System.Collections.Generic;
using engine.world;

namespace builtin.modules.satnav;


/**
 * Contains the factpry for the navmesh inside one fragment.
 *
 * It gathers the specific data for a fragment in joyce specific
 * data and provides a factory for mesh data.
 */
public class FragmentNavMesh : IDisposable
{
    /**
     * If this fragment contains a cluster, this contains the
     * reference.
     */
    private ClusterDesc? _clusterDesc;



    public void Dispose()
    {
    }


    public FragmentNavMesh(Fragment frag)
    {
    }
}