using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ImGuiNET;
using static engine.Logger;

namespace engine.meta;

public class TaskBuilder
{
    /**
     * Build a c# task from the task description in the execution nodes.
     */
    static Task<int> BuildExecTask(ExecDesc ed0)
    {
        bool needChildrenTasks;
        switch (ed0.Mode)
        {
            default:
            case ExecDesc.ExecMode.Constant:
            case ExecDesc.ExecMode.Task:
                needChildrenTasks = false;
                break;
            case ExecDesc.ExecMode.Parallel:
            case ExecDesc.ExecMode.Sequence:
                needChildrenTasks = true;
                break;
        }

        List<Task<int>> listChildrenTasks;
        if (needChildrenTasks)
        {
            listChildrenTasks = new() { Capacity = ed0.Children.Count };
            foreach (var child in ed0.Children)
            {
                listChildrenTasks.Add(BuildExecTask(child));
            }
        }
        else
        {
            listChildrenTasks = null;
        }
            
        switch (ed0.Mode)
        {
            case ExecDesc.ExecMode.Constant:
            default:
                Error($"ExecDesc with type {ed0.Mode} is not supported.");
                return new Task<int>(() => -1);
                break;
            
            case ExecDesc.ExecMode.Task:
                return new Task<int>(new Func<int>(() =>
                {
                    int lastDot = ed0.Implementation.LastIndexOf('.');
                    if (-1 == lastDot)
                    {
                        Error($"Invalid implementation name string \"{ed0.Implementation}\": Does not contain a last dot to mark the method.");
                        return -1;
                    }

                    string className = ed0.Implementation.Substring(0, lastDot);
                    if (className.Length == 0)
                    {
                        Error($"Invalid empty class name \"{ed0.Implementation}\".");
                        return -1;
                    }
                    string methodName = ed0.Implementation.Substring(lastDot + 1);
                    if (methodName.Length == 0)
                    {
                        Error($"Invalid empty method name \"{ed0.Implementation}\".");
                        return -1;
                    }

                    Type t = Type.GetType(className);
                    if (null == t)
                    {
                        Error($"Class \"{className}\" not found.");
                        return -1;
                    }

                    var methodInfo = t.GetMethod(methodName, new Type[] { typeof(IDictionary<string, object>) });
                    if (null == methodInfo)
                    {
                        Error(
                            $"Method \"{methodName}\"(IDictionary<string, object>) not found in class \"{className}\".");
                        return -1;
                    }

                    var r = methodInfo.Invoke(null, new object[1] { new Dictionary<string, object>() });
                    
                    return 0;
                }));
                break;
            case ExecDesc.ExecMode.Parallel:
                return new Task<int>(new Func<int>(() =>
                {
                    var taskAll = Task.WhenAll(listChildrenTasks);
                    try
                    {
                        taskAll.RunSynchronously();
                        if (taskAll.IsCompletedSuccessfully)
                        {
                            return 0;
                        }
                        else
                        {
                            return -1;
                        }
                    }
                    catch (Exception e)
                    {
                        Error($"Running ExecDesc is ${e}.");
                        return -1;
                    }
                }));
                break;
            case ExecDesc.ExecMode.Sequence:
#if true
                return new Task<int>(() =>
                {
                    foreach (var task in listChildrenTasks)
                    {
                        task.RunSynchronously();
                        if (!task.IsCompletedSuccessfully)
                        {
                            return -1;
                        }
                    }
                    return 0;
                });              
#else
            {
/*
 * This might be a fancy way.
 */
                int l = listChildrenTasks.Count;
                if (0 == l)
                {
                    return new Task<int>(() => 0);
                } 
                else if (l == 1)
                {
                    return listChildrenTasks[0];
                }
                else
                {
                    var taskAccumulator = listChildrenTasks[0];
                    for (int i = 1; i < l; ++i)
                    {
                        //taskAccumulator = taskAccumulator.ContinueWith(
                        //    (task, Result) => listChildrenTasks[i], 
                        //    TaskContinuationOptions.OnlyOnRanToCompletion);
                    }
                }
            }
#endif
                
                
                break;
        }
    }
}