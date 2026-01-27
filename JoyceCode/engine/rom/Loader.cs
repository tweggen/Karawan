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

    private static string _strDefaultLoaderAssembly;

    public static void SetDefaultLoaderAssembly(string path)
    {
        _strDefaultLoaderAssembly = path;
    }

    static private bool _traceLoad = false;
    
    
    /// <summary>
    /// Get a list of directories to search for assemblies.
    /// Prioritizes CWD-relative paths over executable directory.
    /// </summary>
    private static IEnumerable<string> _getAssemblySearchPaths()
    {
        // 1. CWD-relative paths (for generic launcher scenarios where CWD is the game project)
        string cwd = Directory.GetCurrentDirectory();
        yield return Path.Combine(cwd, "bin", "Debug", "net9.0");
        yield return Path.Combine(cwd, "bin", "Release", "net9.0");
        yield return Path.Combine(cwd, "bin", "Debug", "net9.0", "osx-arm64");
        yield return Path.Combine(cwd, "bin", "Release", "net9.0", "osx-arm64");
        yield return Path.Combine(cwd, "bin", "Debug", "net9.0", "win-x64");
        yield return Path.Combine(cwd, "bin", "Release", "net9.0", "win-x64");
        yield return cwd;
        
        // 2. Executable directory paths
        var orgExecPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase);
        if (!string.IsNullOrEmpty(orgExecPath))
        {
            string execDirectoryPath;
            if (orgExecPath.StartsWith("file:\\"))
            {
                execDirectoryPath = orgExecPath.Substring(6);
            }
            else if (orgExecPath.StartsWith("file:/"))
            {
                execDirectoryPath = orgExecPath.Substring(5);
            }
            else
            {
                execDirectoryPath = orgExecPath;
            }
            
            yield return execDirectoryPath;
            
            // Android debug override path
            string androidOverridePath = execDirectoryPath.Replace(".__override__", "");
            if (androidOverridePath != execDirectoryPath)
            {
                yield return androidOverridePath;
            }
        }
        
        // 3. AppDomain base directory
        yield return AppDomain.CurrentDomain.BaseDirectory;
    }
    
    
    public static Assembly TryLoadDll(string dllPath)
    {
        if (_traceLoad) Trace($"dllPath = {dllPath}");
        
        // If dllPath is already an absolute path that exists, use it directly
        if (Path.IsPathRooted(dllPath) && File.Exists(dllPath))
        {
            try
            {
                if (_traceLoad) Trace($"Loading from absolute path: {dllPath}");
                return Assembly.LoadFrom(dllPath);
            }
            catch (Exception e)
            {
                Trace($"Unable to load dll from absolute path {dllPath}: {e}");
                ErrorThrow<InvalidOperationException>($"Unable to load dll {dllPath}: {e}");
                return null;
            }
        }
        
        // Search for the dll in various locations
        foreach (var searchDir in _getAssemblySearchPaths())
        {
            string fullPath = Path.Combine(searchDir, dllPath);
            if (_traceLoad) Trace($"Checking: {fullPath}");
            
            if (File.Exists(fullPath))
            {
                try
                {
                    if (_traceLoad) Trace($"Found and loading: {fullPath}");
                    return Assembly.LoadFrom(fullPath);
                }
                catch (Exception e)
                {
                    Trace($"Unable to load dll {dllPath} from {fullPath}: {e}");
                    // Continue searching in other paths
                }
            }
        }
        
        // If not found in any search path, report error
        Error($"Unable to find {dllPath} in any search path.");
        
        // Try one more time with the original path (let OS loader decide)
        try
        {
            return Assembly.LoadFrom(dllPath);
        }
        catch (Exception e)
        {
            Trace($"Unable to load dll {dllPath}: {e}");
            ErrorThrow<InvalidOperationException>($"Unable to load dll {dllPath}: {e}");
        }

        return null;
    }

    
    private static SortedDictionary<string, Assembly> _visited = new ();
    
    
    public static System.Reflection.Assembly[] GetAllAssemblies(string dllPath)
    {
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
                if (_visited.ContainsKey(asm.GetName().Name))
                {
                    continue;
                }

                if (_traceLoad) Trace($"Adding dll {asm.GetName().Name}");
                _visited.Add(asm.GetName().Name, asm);
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
            try
            {
                Assembly asm = TryLoadDll(dllPath);

                if (null != asm)
                {
                    type = asm.GetType(fullClassName);
                }
            }
            catch (Exception e)
            {
                Trace($"Error with dll loading, falling back to scanning all assemblies. ({dllPath}: {e})");
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
    
    
    public static Type LoadType(string fullClassName)
    {
        return LoadType(_strDefaultLoaderAssembly, fullClassName);
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


    public static object? LoadClass(string fullClassName)
    {
        return LoadClass(_strDefaultLoaderAssembly, fullClassName);
    }
}
