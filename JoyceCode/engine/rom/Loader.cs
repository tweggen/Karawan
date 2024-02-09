using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Serialization;
using static engine.Logger;

namespace engine.rom;

public class Loader
{
    static private SortedDictionary<string, Assembly> _mapAlreadyLoaded = new SortedDictionary<string, Assembly>();
    static private SortedSet<string> _setLoadFailed = new SortedSet<string>();

    static private bool _traceLoad = false;
    public static Assembly TryLoadDll(string dllPath)
    {
        if (_traceLoad) Trace($"dllPath = {dllPath}");
        try
        {
            bool foundDll = false;

            var orgExecPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase);
            if (_traceLoad) Trace($"orgExecPath = {orgExecPath}");
            string execDirectoryPath = "";
            if (null == orgExecPath)
            {
                orgExecPath = "";
            }
            else
            {
                /*
                 * Remove the url type head from the name. This considers both linux and windows
                 * type of names.
                 */
                execDirectoryPath = orgExecPath.Replace("file:/", "").Replace("file:\\", "");
            }
            if (_traceLoad) Trace($"execDirectoryPath = {execDirectoryPath}");


            /*
             * If we couldn't find it by its given name, look in the debug override path
             * (for android).
             */
            if (!File.Exists(Path.Combine(execDirectoryPath, dllPath)))
            {
                execDirectoryPath = execDirectoryPath?.Replace(".__override__", "");
            }
            if (_traceLoad) Trace($"execDirectoryPath = {execDirectoryPath}");

            /*
             * Test first, if we can find the file.
             */
            string pathToLookFirst = Path.Combine(execDirectoryPath, dllPath); 
            if (File.Exists(pathToLookFirst))
            {
                foundDll = true;
            }
            else
            {
                Error($"Unable to find {dllPath} in {pathToLookFirst}.");
                
                /*
                 * Even though, let the OS loader decide whether it really can't be found.
                 */
            }

            Assembly asm = Assembly.LoadFrom(pathToLookFirst);
            
            return asm;
        }
        catch (Exception e)
        {
            Trace($"Unable to load dll {dllPath}: {e}");
        }

        return null;
    }
    
    
    public static System.Reflection.Assembly[] GetAllAssemblies(string dllPath)
    {
        var visited = new SortedDictionary<string, Assembly>();
        var queue = new Queue<Assembly>();

        try
        {
            /*
             * Load the given assembly if not yet done.
             */
            try
            {
                if (!_mapAlreadyLoaded.ContainsKey(dllPath))
                {
                    /*
                     * Add both the user name and the real name.
                     */
                    var asmGiven = TryLoadDll(dllPath);
                    if (null != asmGiven)
                    {
                        _mapAlreadyLoaded.Add(asmGiven.GetName().Name, asmGiven);
                        _mapAlreadyLoaded.Add(dllPath, asmGiven);
                        queue.Enqueue(asmGiven);
                    }
                }
            }
            catch (Exception e)
            {
                Trace($"Unable to load the given assembly {dllPath} into the desired context: {e}");
            }

            /*
             * In addition, queue the entry assmebly...
             */
            Assembly asmEntry = Assembly.GetEntryAssembly();
            if (null != asmEntry)
            {
                if (!_mapAlreadyLoaded.ContainsKey(asmEntry.GetName().Name))
                {
                    _mapAlreadyLoaded.Add(asmEntry.GetName().Name, asmEntry);
                    queue.Enqueue(asmEntry);
                }
            }

            /*
             * ... and all other known assemblies.
             */
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (null != asm)
                {
                    if (!_mapAlreadyLoaded.ContainsKey(asm.GetName().Name))
                    {
                        _mapAlreadyLoaded.Add(asm.GetName().Name, asm);
                        queue.Enqueue(asm);
                    }
                }
            }

            /*
             * Finally, dump all the assemblies we use to find the target.
             */
            foreach (var asm in queue)
            {
                if (_traceLoad) Trace($"Starting resolve with {asm.GetName().Name}");
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
                if (visited.ContainsKey(asm.GetName().Name))
                {
                    continue;
                }

                if (_traceLoad) Trace($"Adding dll {asm.GetName().Name}");
                visited.Add(asm.GetName().Name, asm);
                var references = asm.GetReferencedAssemblies();
                foreach (var anRef in references)
                {
                    if (_setLoadFailed.Contains(anRef.Name))
                    {
                        /*
                         * Skip re-load of already failed load.
                         */
                        continue;
                    }

                    /*
                     * Try to load the assembly if not yet loaded.
                     */
                    if (!_mapAlreadyLoaded.ContainsKey(anRef.Name))
                    {
                        try
                        {
                            var asmRef = Assembly.Load(anRef);
                            _mapAlreadyLoaded.Add(anRef.Name, asmRef);
                        }
                        catch (Exception ex)
                        {
                            Trace($"Failed to load {anRef.Name}: {ex}");
                            _setLoadFailed.Add(anRef.Name);
                        }
                    }

                    /*
                     * If we could load it, queue it's dependencies to be analysed.
                     */
                    if (_mapAlreadyLoaded.ContainsKey(anRef.Name))
                    {
                        queue.Enqueue(Assembly.Load(anRef));
                    }
                }
            }

        }
        catch (Exception e)
        {
            Trace($"Exception {e}");
        }

        // .. Fallback
        /*
         * Finally, return everything that has been loaded.
         */
        return _mapAlreadyLoaded.Values.ToArray();
    }


    /**
     * Return the given type that is supposed to be in the given dll.
     *
     * First we check if we can find the dll by various attempts.
     */
    public static Type LoadType(string dllPath, string fullClassName)
    {
        Type type = null;
        try
        {
            Assembly asm = TryLoadDll(dllPath);

            if (null != asm)
            {
                type = asm.GetType(fullClassName);
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