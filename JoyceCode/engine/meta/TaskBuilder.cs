#if false
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using ImGuiNET;
using static engine.Logger;

namespace engine.meta;

public class TaskBuilder
{
    /**
     * Build a c# task from the task description in the execution nodes.
     */
    public static Task BuildExecTask(
        ExecDesc ed0, 
        IDictionary<string, object> overallParams,
        IDictionary<string, IEnumerable<object>> applyParameters)
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
            case ExecDesc.ExecMode.ApplyParallel:
                needChildrenTasks = true;
                break;
        }

        List<Task> listChildrenTasks;
        if (needChildrenTasks)
        {
            listChildrenTasks = new() { Capacity = ed0.Children.Count };
            foreach (var child in ed0.Children)
            {
                listChildrenTasks.Add(BuildExecTask(child, overallParams, applyParameters));
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
            {
                break;
            }

            case ExecDesc.ExecMode.Parallel:
                return new Task(new Action(async () =>
                {
                    var taskAll = Task.WhenAll(listChildrenTasks);
                    try
                    {
                        taskAll.Start();
                        await taskAll;
                        if (taskAll.IsCompletedSuccessfully)
                        {
                        }
                        else
                        {
                            ErrorThrow("Failed to run all tasks.", m => new InvalidOperationException(m));
                        }
                    }
                    catch (Exception e)
                    {
                        ErrorThrow($"Running ExecDesc is ${e}.", m => new InvalidOperationException(m));
                    }
                }));
                break;
            
            case ExecDesc.ExecMode.ApplyParallel:
                if (listChildrenTasks.Count != 1)
                {
                    ErrorThrow("Excepting exactly one child for a ApplyParallel ExecDesc.", m => new ArgumentException(m));
                }

                IEnumerable<object> listToApply = applyParameters[ed0.Selector];

                List<Task> listAllTasks = new();
                foreach (var applyParam in listToApply)
                {
                    var p = new Dictionary<string, object>(overallParams);
                    p[ed0.Target] = applyParam;

                    Task tChild = BuildExecTask(ed0.Children[0], p, applyParameters);
                    listAllTasks.Add(tChild);
                }
                    
                return new Task(new Action(() =>
                {
                    var taskAll = Task.WhenAll(listAllTasks);
                    taskAll.Start();
                    try
                    {
                        taskAll.Wait();
                        if (taskAll.IsCompletedSuccessfully)
                        {
                            return;
                        }
                        else
                        {
                            ErrorThrow("Failed to run all tasks.", m => new InvalidOperationException(m));
                        }
                    }
                    catch (Exception e)
                    {
                        ErrorThrow($"Running ExecDesc is ${e}.", m => new InvalidOperationException(m));
                    }
                }));
                break;
            
            case ExecDesc.ExecMode.Sequence:
#if true
                return new Task(new Action(async () =>
                {
                    foreach (var task in listChildrenTasks)
                    {
                        task.Start();
                        await task;
                        if (!task.IsCompletedSuccessfully)
                        {
                            ErrorThrow("Failed to run all tasks.", m => new InvalidOperationException(m));
                        }
                    }
                }));
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
#endif