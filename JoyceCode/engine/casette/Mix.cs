using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using static engine.Logger;

namespace engine.casette;


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

    public void GetTree(string path, Action<JsonNode?> actParse)
    {
        actParse(_view.GetMergedSubtree(path));
    }
    
    
    public JsonNode? GetTree(string path)
    {
        return _view.GetMergedSubtree(path);
    }
    

    public void RemoveFragment(string rootPath, int priority = 0)
    {
        _view.Remove(rootPath, priority);
    }


    private void _upsertIncludes(string rootPath, JsonElement je, int priority)
    {
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
                        string tag = null;
                        if (string.IsNullOrEmpty(tag))
                        {
                            int idx = includePath.LastIndexOf('/');
                            if (idx != -1 && idx != includePath.Length - 1)
                            {
                                tag = includePath[(idx + 1)..];
                            }
                            else
                            {
                                tag = includePath;
                            }
                        }
                        string pathProbe = Path.Combine(engine.GlobalSettings.Get("Engine.ResourcePath"), includePath);
                        if (!File.Exists(pathProbe))
                        {
                            Trace($"Warning: include file for {pathProbe} does not exist.");
                        }
                        engine.Assets.AddAssociation(tag!, includePath);
                        
                        string jsonCompletePath = Path.Combine(Directory, includePath);
                        if (engine.Assets.Exists(jsonCompletePath))
                        {
                            Stream fs = default;
                            JsonDocument doc = default;
                            try
                            {
                                fs = engine.Assets.Open(jsonCompletePath);
                                AdditionalFiles.Add(jsonCompletePath);
                                doc = JsonDocument.Parse(fs);
                                Trace("Adding include file "+includePath+ " at "+ jsonCompletePath);
                                // Upsert the loaded fragment over the current path
                                _view.Upsert(currentPath, doc.RootElement, priority);
                            }
                            catch (Exception _)
                            {
                                Trace("Unable to open include file "+includePath+" at "+jsonCompletePath);
                            }
                            finally
                            {
                                fs?.Dispose();
                                doc?.Dispose();
                            }
                        } else {
                            Trace("include path does not exist "+jsonCompletePath);     
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
}