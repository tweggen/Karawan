using System.Configuration;
using System.Diagnostics;
using System.Text.Json;

namespace CmdLine;

public class GameConfig
{
    private JsonElement _jeRoot;

    public SortedDictionary<string, Resource> MapResources = new();
    
    
    public void LoadResourceList(JsonElement je)
    {
        try
        {
            foreach (var jeRes in je.EnumerateArray())
            {
                string? uri = jeRes.GetProperty("uri").GetString();
                if (null == uri)
                {
                    throw new InvalidDataException("no uri specified in resource.");
                }

                string? tag = null;
                if (jeRes.TryGetProperty("tag", out var jpTag))
                {
                    tag = jpTag.GetString();
                }
                if (null == tag)
                {
                    int idx = uri.LastIndexOf('/');
                    if (idx != -1 && idx != uri.Length - 1)
                    {
                        tag = uri[(idx + 1)..];
                    }
                    else
                    {
                        tag = uri;
                    }
                }

                Console.Error.Write($"GameConfig: Added Resource \"{tag}\" from {uri}.");
                MapResources[tag] = new Resource() { Uri = uri };
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Error loading resource: {e}");
        }

    }
    
    
    public void LoadResources(JsonElement je)
    {
        try
        {
            if (je.TryGetProperty("list", out var jeResourceList))
            {
                LoadResourceList(jeResourceList);
            }


        }
        catch (Exception e)
        {
            
        }
    }

    public void LoadGameConfig(JsonElement je)
    {
        if (je.TryGetProperty("resources", out var jeResources))
        {
            LoadResources(jeResources);
        }
    }

    
    private void _loadGameConfigFile(string jsonPath)
    {
        JsonDocument jdocGame = JsonDocument.Parse(new FileStream(jsonPath, FileMode.Open), new()
        {
            AllowTrailingCommas = true
        });
        _jeRoot = jdocGame.RootElement;
        LoadGameConfig(_jeRoot);
    }


    public GameConfig(string jsonPath)
    {
        _loadGameConfigFile(jsonPath);
    }

}