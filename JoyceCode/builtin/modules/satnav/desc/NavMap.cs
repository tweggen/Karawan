using System;
using static engine.Logger;

namespace builtin.modules.satnav.desc;

public class NavMap
{
    private object _lo = new();

    private NavCluster _topCluster;
    public NavCluster TopCluster
    {
        get {
            lock (_lo)
            {
                if (null == _topCluster)
                {
                    ErrorThrow<InvalidOperationException>($"No top cluster has been setup yet.");
                }
                return _topCluster;
            }
        }

        
        set
        {
            lock (_lo)
            {
                if (null != _topCluster)
                {
                    ErrorThrow<InvalidOperationException>($"No top cluster already had been setup yet.");
                }
                _topCluster = value;
            }
        }
    }

    public NavMap()
    {
    }
}