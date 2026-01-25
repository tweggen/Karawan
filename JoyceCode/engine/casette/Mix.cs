using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using static engine.Logger;

namespace engine.casette;


/// <summary>
/// Default file provider that uses engine.Assets for file access.
/// Used when running in the engine context.
/// </summary>
public class EngineAssetFileProvider : IMixFileProvider
{
    public bool Exists(string path)
    {
        return engine.Assets.Exists(path);
    }
    
    public Stream Open(string path)
    {
        return engine.Assets.Open(path);
    }
    
    public void AddAssociation(string key, string path)
    {
        engine.Assets.AddAssociation(key, path);
    }
    
    public string ResolvePath(string relativePath)
    {
        return Path.Combine(engine.GlobalSettings.Get("Engine.ResourcePath"), relativePath);
    }
}


/**
 * This is the central configuration manager.
 * Setup code can add or remove layers of configuration, clients can subscribe
 * to a certain subtree.
 */
public class Mix
{
    private View _view = new();

    public string Directory = "";

    public HashSet<string> AdditionalFiles = new HashSet<string>();
    
    /// <summary>
    /// Optional file provider for loading include files.
    /// If null, includes are not automatically resolved.
    /// </summary>
    public IMixFileProvider? FileProvider { get; set; }

    public void GetTree(string path, Action<JsonNode?> actParse)
    {
        actParse(_view.GetMergedSubtree(path));
    }
    
    
    public JsonNode? GetTree(string path)
    {
        return _view.GetMergedSubtree(path);
    }
    
    
    /// <summary>
    /// Subscribe to changes at a specific path.
    /// </summary>
    public IDisposable Subscribe(string path, Action<View.DomChangeEvent> handler)
    {
        return _view.Subscribe(path, handler);
    }
    

    public void RemoveFragment(string rootPath, int priority = 0)
    {
        _view.Remove(rootPath, priority);
    }


    private void _upsertIncludes(string rootPath, JsonElement je, int priority)
    {
        // Skip include processing if no file provider is set
        if (FileProvider == null)
        {
            return;
        }
        
        var queue = new Queue<(string Path, JsonElement Element)>();
        queue.Enqueue((rootPath, je));

        while (queue.Count > 0)
        {
            var (currentPath, currentElement) = queue.Dequeue();
            Trace("Analysing path "+currentPath);

            if (currentElement.ValueKind == JsonValueKind.Object)
            {
                // Check for __include__ attribute
                if (currentElement.TryGetProperty("__include__", out var includeProp) &&
                    includeProp.ValueKind == JsonValueKind.String)
                {
                    Trace("have include property");
                    var includePath = includeProp.GetString();
                    if (!string.IsNullOrEmpty(includePath))
                    {
                        /*
                         * This is the complete path relative to the mix module
                         */
                        string jsonCompletePath = Path.Combine(Directory, includePath);

                        /*
                         * Resolve to full path using the file provider
                         */
                        string pathProbe = FileProvider.ResolvePath(jsonCompletePath);
                        
                        /*
                         * Sneak if the resource exists?
                         */
                        if (!File.Exists(pathProbe))
                        {
                            Trace($"Warning: include file for {pathProbe} does not exist.");
                        }
                        else
                        {
                            /*
                             * Just by referencing it, we add it to the list of associations (hack...)-
                             * TXWTODO: We need  this on platforms that do not read a pre-compiled list of
                             * assets.
                             */
                            FileProvider.AddAssociation(includePath, jsonCompletePath);
                        
                        }
                        
                        /*
                         * Currently, we only accept an open call with the file name only
                         */
                        string fileNameOnly = Path.GetFileName(includePath);
                        Trace($"Trying to open file name {fileNameOnly}");
                        
                        if (FileProvider.Exists(fileNameOnly))
                        {
                            Stream fs = default;
                            JsonDocument doc = default;
                            try
                            {
                                fs = FileProvider.Open(fileNameOnly);
                                AdditionalFiles.Add(jsonCompletePath);
                                doc = JsonDocument.Parse(fs);
                                Trace("Adding include file "+includePath+ " at "+ jsonCompletePath);
                                // Upsert the loaded fragment over the current path
                                _view.Upsert(currentPath, doc.RootElement, priority);
                            }
                            catch (Exception _)
                            {
                                Trace("Unable to open include file "+fileNameOnly+" at "+jsonCompletePath);
                            }
                            finally
                            {
                                fs?.Dispose();
                                doc?.Dispose();
                            }
                        } else {
                            Trace("include path does not exist "+fileNameOnly);     
                        }                       
                    } else {
                        Trace("include property null.");                            
                    }
                }

                // Enqueue children
                foreach (var prop in currentElement.EnumerateObject())
                {
                    var childPath = currentPath.EndsWith("/")
                        ? currentPath + prop.Name
                        : currentPath + "/" + prop.Name;
                    queue.Enqueue((childPath, prop.Value));
                }
            }
            else if (currentElement.ValueKind == JsonValueKind.Array)
            {
                int idx = 0;
                foreach (var item in currentElement.EnumerateArray())
                {
                    var childPath = $"{currentPath}/{idx}";
                    queue.Enqueue((childPath, item));
                    idx++;
                }
            }
            // Primitive values have no children
        }
    }
    

    /**
     * Add the given fragment, preprocess it, then resolve any includes.
     */
    private void _preprocessUpsert(string rootPath, JsonElement je, int priority = 0)
    {
        _view.Upsert(rootPath, je, priority);

        _upsertIncludes(rootPath, je, priority);
    }
    
    
    public void UpsertFragment(string rootPath, JsonElement je, int priority = 0)
    {
        _preprocessUpsert(rootPath, je, priority);
    }
    
    public void UpsertFragment(string rootPath, System.IO.Stream stream, int priority = 0)
    {
        JsonDocument jdoc = JsonDocument.Parse(stream, new()
        {
            AllowTrailingCommas = true
        });
        
        _preprocessUpsert(rootPath, jdoc.RootElement, priority);
    }


    public Mix()
    {
    }
    
    
    /// <summary>
    /// Create a Mix with a specific file provider.
    /// </summary>
    public Mix(IMixFileProvider fileProvider)
    {
        FileProvider = fileProvider;
    }
}
