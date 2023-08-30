using System.Collections.Generic;
using System.Security.Cryptography;

namespace engine.meta;

public class ExecScope
{
    public IDictionary<string, object> OverallParams;
    public IDictionary<string, IEnumerable<object>> ApplyParameters;


    private static IDictionary<string, object> _mergeParams(IDictionary<string, object> pParent, IDictionary<string, object> pNew)
    {
        if (null == pParent)
        {
            if (null == pNew)
            {
                return null;
            }
            else
            {
                return new Dictionary<string, object>(pNew);
            }
        }
        else
        {
            if (null == pNew)
            {
                return new Dictionary<string, object>(pParent);
            }
            else
            {
                var p = new Dictionary<string, object>(pParent);
                foreach (var kvp in pNew)
                {
                    p[kvp.Key] = kvp.Value;
                }

                return p;
            }

        }
    }
    
    
    public ExecScope(IDictionary<string, object> newParams, IDictionary<string, IEnumerable<object>> newApplyParams)
    {
        OverallParams = newParams;
        ApplyParameters = newApplyParams;
    }


    public ExecScope(ExecScope esParent, IDictionary<string, object> newParams)
    {
        OverallParams = _mergeParams(esParent.OverallParams, newParams);
        ApplyParameters = esParent.ApplyParameters;
    }
    
    
    public ExecScope()
    {
        OverallParams = new Dictionary<string, object>();
        ApplyParameters = new Dictionary<string, IEnumerable<object>>();
    }
}