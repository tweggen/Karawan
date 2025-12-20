using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using static engine.Logger;

namespace engine.meta;

public class ExecTaskNode : AExecNode
{
    private object _instance = null;

    
    public override Task? Execute(Func<object, Task?> op)
    {
        if (ExecDesc.ConfigCondition != null)
        {
            object propValue = Props.Get(ExecDesc.ConfigCondition, true);
            if (propValue is bool)
            {
                if (((bool)propValue) != true)
                {
                    return null;
                }
            }
            if (propValue is string)
            {
                if (((string)propValue) != "true")
                {
                    return null;
                }
            }
        }
        
        // Trace($"Starting node {ExecDesc.Implementation} with {_instance}");
        Task? t = op(_instance);
        return t;
    }

    
    public ExecTaskNode(ExecDesc ed0, ExecScope esParent) : base(ed0, esParent)
    {
        ExecDesc = ed0;
        
        int lastDot = ed0.Implementation.LastIndexOf('.');
        if (-1 == lastDot)
        {
            ErrorThrow(
                $"Invalid implementation name string \"{ed0.Implementation}\": Does not contain a last dot to mark the method.",
                m => new ArgumentException(m));
        }

        string className = ed0.Implementation.Substring(0, lastDot);
        if (className.Length == 0)
        {
            ErrorThrow($"Invalid empty class name \"{ed0.Implementation}\".",
                m => new ArgumentException(m));
        }

        string methodName = ed0.Implementation.Substring(lastDot + 1);
        if (methodName.Length == 0)
        {
            ErrorThrow($"Invalid empty method name \"{ed0.Implementation}\".",
                m => new ArgumentException(m));
        }

        // TXWTODO: We need to implement a generic type name lookup. and cache it!!!!!
#if true
        Type t = Type.GetType(className);
        if (t == null)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = a.GetType(className);
                if (t != null)
                {
                    break;
                }
            }
        }
#else
        Type t = Type.GetType(className);
#endif
        if (null == t)
        {
            ErrorThrow($"Class \"{className}\" not found.",
                m => new ArgumentException(m));
        }

        var methodInfo = t.GetMethod(methodName, new Type[] { typeof(IDictionary<string, object>) });
        if (null == methodInfo)
        {
            ErrorThrow(
                $"Method \"{methodName}\"(IDictionary<string, object>) not found in class \"{className}\".",
                m => new ArgumentException(m));
        }
        
        /*
         * Finally, create the instance of the object we shall call.
         */
        _instance = methodInfo.Invoke(null, new object[1] { this.ExecScope.OverallParams });
    }

}