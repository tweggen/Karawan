using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using static engine.Logger;

namespace engine.rom;

public class Loader
{
    static private SortedDictionary<string, Assembly> _mapAlreadyLoaded = new SortedDictionary<string, Assembly>();
    static private SortedSet<string> _setLoadFailed = new SortedSet<string>();
    
    
    public static System.Reflection.Assembly[] GetAllAssemblies(string dllPath)
    {
        var visited = new SortedDictionary<string, Assembly>();
        var queue = new Queue<Assembly>();
        
        
        /*
         * Load the given assembly if not yet done.
         */
        try
        {
            var asmGiven = Assembly.Load(dllPath);
            queue.Enqueue(asmGiven);
        }
        catch (Exception e)
        {
            Trace($"Unable to load the given assembly {dllPath} into the desired context: {e}");
        }
        queue.Enqueue(Assembly.GetEntryAssembly());
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            _mapAlreadyLoaded.Add(asm.FullName, asm);
            queue.Enqueue(asm);
        }
        foreach (var asm in queue)
        {
            Trace($"Starting with {asm.FullName}");
        }


        /*
         * Iterate through all dlls we should load and check.
         */
        while (queue.Any())
        {
            var asm = queue.Dequeue();

            /*
             * If we already traced its dependencies, ignore it.
             */
            if (visited.ContainsKey(asm.FullName))
            {
                continue;
            }

            Trace($"Adding dll {asm.FullName}");
            visited.Add(asm.GetName().FullName, asm);
            var references = asm.GetReferencedAssemblies();
            foreach (var anRef in references)
            {
                if (_setLoadFailed.Contains(anRef.FullName))
                {
                    /*
                     * Skip re-load of already failed load.
                     */
                    continue;
                }

                /*
                 * Try to load the assembly if not yet loaded.
                 */
                if (!_mapAlreadyLoaded.ContainsKey(anRef.FullName))
                {
                    try
                    {
                        var asmRef = Assembly.Load(anRef);
                        _mapAlreadyLoaded.Add(anRef.FullName, asmRef);
                    }
                    catch (Exception ex)
                    {
                        Trace($"Failed to load {anRef.FullName}: {ex}");
                        _setLoadFailed.Add(anRef.FullName);
                    }
                }

                /*
                 * If we could load it, queue it's dependencies to be analysed.
                 */
                if (_mapAlreadyLoaded.ContainsKey(anRef.FullName))
                {
                    queue.Enqueue(Assembly.Load(anRef));
                }
            }
        }
        return visited.Values.ToArray();
    }


    public static Type LoadType(string dllPath, string fullClassName)
    {
        try
        {
            bool foundDll = false;
            var execDirectoryPath = 
                Path.GetDirectoryName(
                        System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase)?
                    .Replace("file:/", "")
                    .Replace("file:\\", "")
                ;
            
            if (!File.Exists(Path.Combine(execDirectoryPath, dllPath)))
            {
                execDirectoryPath = execDirectoryPath?.Replace(".__override__", "");
            }

            if (false && File.Exists(Path.Combine(execDirectoryPath, dllPath)))
            {
                foundDll = true;
            } else
            {
                Error($"Unable to find {dllPath}.");
            }

            Type type = null;
            if (foundDll)
            {
                Assembly asm = Assembly.LoadFrom(Path.Combine(execDirectoryPath, dllPath));
                if (null != asm)
                {
                    type = asm.GetType(fullClassName);
                }
            }
            if (null == type)
            {
                System.Reflection.Assembly[] allAssemblies = GetAllAssemblies(dllPath);
                foreach (var asmCurr in allAssemblies)
                {
                    type = asmCurr.GetType(fullClassName);
                    if (null != type)
                    {
                        break;
                    }
                }
            }
    
            return type;
        }
        catch (Exception e)
        {
            Warning($"Unable to load class {fullClassName} from assembly {dllPath}.");
        }

        return null;

    }

    public static object? LoadClass(string dllPath, string fullClassName)
    {
        try
        {
            Type type = LoadType(dllPath, fullClassName); 
            if (null != type)
            {
                object instance = Activator.CreateInstance(type);
                return instance;
            }
        }
        catch (Exception e)
        {
            Warning($"Unable to load class {fullClassName} from assembly {dllPath}.");
        }

        return null;
    }
}